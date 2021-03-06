using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using STM.Implementation.Lockbased;
using System.Collections.Immutable;
using System.Collections.Generic;
using Evaluation.Common;


namespace LanguagedBasedHashMap
{
    public class Program
    {
        public static void Main()
        {
            var map = new StmHashMap<int, int>();
            TestMap(map);
            map = new StmHashMap<int, int>();
            TestMapConcurrent(map);
        }


        private static void TestMapConcurrent(IMap<int, int> map)
        {
            const int t1From = 0;
            const int t1To = 1000;
            const int t2From = -1000;
            const int t2To = 0;
            const int expectedSize = 2000;

            var t1 = new Thread(() => MapAdd(map, t1From, t1To));
            var t2 = new Thread(() => MapAdd(map, t2From, t2To));

            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
            Debug.Assert(expectedSize == map.Count);

            t1 = new Thread(() => MapAddIfAbsent(map, t1From, t1To));
            t2 = new Thread(() => MapAddIfAbsent(map, t2From, t2To));

            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
            Debug.Assert(expectedSize == map.Count);

            t1 = new Thread(() => MapGet(map, t1From, t1To));
            t2 = new Thread(() => MapGet(map, t2From, t2To));
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            t1 = new Thread(() => MapForeach(map));
            t2 = new Thread(() => MapForeach(map));
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            t1 = new Thread(() => MapRemove(map, t1From, t1To));
            t2 = new Thread(() => MapRemove(map, t2From, t2To));

            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            Debug.Assert(0 == map.Count);
        }

        private static void TestMap(IMap<int, int> map)
        {
            const int from = -50;
            const int to = 50;

            Debug.Assert(map.Count == 0);
            MapAdd(map, from, to);
            Debug.Assert(map.Count == 100);

            MapAddIfAbsent(map, from, to);
            Debug.Assert(map.Count == 100);

            MapGet(map, from, to);

            MapRemove(map, from, to);
            Debug.Assert(map.Count == 0);
        }


        public static void MapAdd(IMap<int, int> map, int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                map[i] = i;
            }
        }

        public static void MapAddIfAbsent(IMap<int, int> map, int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                map.AddIfAbsent(i, i);
            }
        }

        public static void MapRemove(IMap<int, int> map, int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                map.Remove(i);
            }
        }

        public static void MapGet(IMap<int, int> map, int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                Debug.Assert(i == map.Get(i));
            }
        }

        public static void MapForeach(IMap<int, int> map)
        {
            foreach (var kvPair in map)
            {
                Debug.Assert(kvPair.Key == kvPair.Value);
            }
        }
    }



    public class StmHashMap<K,V> : BaseHashMap<K,V>
    {
        private atomic Bucket[] _buckets;
        private atomic int _threshold;
        private atomic int _size;

        public StmHashMap() : this(DefaultNrBuckets)
        {

        }

        public StmHashMap(int nrBuckets)
        {
            _buckets = MakeBuckets(nrBuckets);
            _threshold = CalculateThreshold(nrBuckets);
        }

        private Bucket[] MakeBuckets(int nrBuckets)
        {
            var temp = new Bucket[nrBuckets];
            for (int i = 0; i < nrBuckets; i++)
            {
                temp[i] = new Bucket();
            }
            return temp;
        }

        #region Utility

        private Node CreateNode(K key, V value)
        {
            return new Node(key, value);
        }

        private int GetBucketIndex(K key)
        {
            return GetBucketIndex(_buckets.Length, key);
        }

        private Node FindNode(K key)
        {
            return FindNode(key, GetBucketIndex(key));
        }

        private Node FindNode(K key, int bucketIndex)
        {
            return FindNode(key, _buckets[bucketIndex].Value);
        }

        private Node FindNode(K key, ImmutableList<Node> chain)
        {
            return chain.Find(n => n.Key.Equals(key));
        }

        #endregion Utility

        public override bool ContainsKey(K key)
        {
            return FindNode(key) != null;
        }

        public override V Get(K key)
        {
            atomic
            {
                var node = FindNode(key);
                if(node == null)
                {
                    //If node == null key is not present in dictionary
                    throw new KeyNotFoundException("Key not found. Key: " + key);
                }
                return node.Value;
            }
        }

        public override void Add(K key, V value)
        {
            atomic
            {
                var bucketIndex = GetBucketIndex(key);
                //TMVar wrapping the immutable chain list
                var bucketVar = _buckets[bucketIndex];
                var node = FindNode(key, bucketVar.Value);
               
                if (node != null)
                {
                    //If node is not null key exist in map. Update the value
                    node.Value = value;
                }
                else
                {
                    //Else insert the node
                    bucketVar.Value = bucketVar.Value.Add(CreateNode(key, value));
                    _size++;
                    ResizeIfNeeded();
                }
            }
        }

        public override bool AddIfAbsent(K key, V value)
        {
            atomic
            {
                var bucketIndex = GetBucketIndex(key);
                //TMVar wrapping the immutable chain list
                var bucketVar = _buckets[bucketIndex];
                var node = FindNode(key, bucketVar.Value);

                if (node == null)
                {
                    //If node is not found key does not exist so insert
                    bucketVar.Value = bucketVar.Value.Add(CreateNode(key, value));
                    _size++;
                    ResizeIfNeeded();
                    return true;
                }

                return false;
            }
        }
        private void ResizeIfNeeded()
        {
            if (_size >= _threshold)
            {
                Resize();
            }
        }

        private void Resize()
        {
            //Construct new backing array
            var newBucketSize = _buckets.Length * 2;
            var newBuckets = MakeBuckets(newBucketSize);

            //For each key in the map rehash
            for (var i = 0; i < _buckets.Length; i++)
            {
                var bucket = _buckets[i];
                foreach (var node in bucket.Value)
                {
                    var bucketIndex = GetBucketIndex(newBucketSize, node.Key);
                    newBuckets[bucketIndex].Value = newBuckets[bucketIndex].Value.Add(node);
                }
            }

            //Calculate new resize threshold and assign the rehashed backing array
            _threshold = CalculateThreshold(newBucketSize);
            _buckets = newBuckets;
        }

        public override bool Remove(K key)
        {
            atomic
            {
                var bucketIndex = GetBucketIndex(key);
                //TMVar wrapping the immutable chain list
                var bucketVar = _buckets[bucketIndex];
                var node = FindNode(key, bucketVar.Value);

                if (node != null)
                {
                    //If node is not found key does not exist so insert
                    bucketVar.Value = bucketVar.Value.Remove(node);
                    _size--;
                    return true;
                }

                return false;
            }
        }

        private IEnumerator<KeyValuePair<K, V>> BuildEnumerator()
        {
            Bucket[] backingArray = _buckets;
            Thread.MemoryBarrier();
            //Thread.MemoryBarrier();  Forces the compiler to not move the local variable into the loop header
            //This is important as the iterator will otherwise start iterating over a resized backing array 
            // if a resize happes during iteration.
            //Result if allowed could be the same key value pair being iterated over more than once or not at all
            //This way the iterator only iterates over one backing array if a resize occurs those changes are not taken into account
            //Additions or removals are still possible during iteration => same guarantee as System.Collections.Concurrent.ConcurrentDictionary
            for (var i = 0; i < backingArray.Length; i++)
            {
                var bucket = backingArray[i];
                foreach (var node in bucket.Value)
                {
                    yield return new KeyValuePair<K, V>(node.Key, node.Value);
                }
            }
        }

        public override IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            atomic
            {
                var list = new List<KeyValuePair<K, V>>(_size);
                for (var i = 0; i < _buckets.Length; i++)
                {
                    var bucket = _buckets[i];
                    foreach (var node in bucket.Value)
                    {
                        var keyValuePair = new KeyValuePair<K, V>(node.Key, node.Value);
                        list.Add(keyValuePair);
                    }
                }
                return list.GetEnumerator();
            }
        }


        public override V this[K key]
        {
            get { return Get(key); }
            set { Add(key, value); }
        }

        public override int Count
        {
            get { return _size; }
        }

        private class Bucket
        {
            public atomic ImmutableList<Node> Value { get; set; }
            public Bucket()
            {
                Value = ImmutableList.Create<Node>();
            }
        }

        private class Node
        {
            public K Key { get; private set; }
            public atomic V Value { get; set; }

            public Node(K key, V value)
            {
                Key = key;
                Value = value;
            }
        }
    }

}