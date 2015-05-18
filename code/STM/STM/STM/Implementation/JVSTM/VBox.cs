﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using STM.Implementation.Common;

namespace STM.Implementation.JVSTM
{
    public class VBox<T> : BaseVBox
    {
        private volatile VBoxBody<T> _body;
        private ImmutableList<IRetryLatch> _listeners = ImmutableList<IRetryLatch>.Empty; 

        public VBox() : this(default(T))
        {
            
        }

        public VBox(T value)
        {
            _body = new VBoxBody<T>(value, 0, null);
        }

        public T Read(JVTransaction transaction)
        {
            if (transaction.WriteMap.Contains(this))
            {
                return (T)transaction.WriteMap[this];
            }

            var body = _body;
            var tNumber = transaction.Number;
            while (body.Version > tNumber)
            {
                body = body.Next;
            }

            transaction.ReadMap.Put(this,body);

            return body.Value;
        }

        public void Put(JVTransaction transaction, T value)
        {
            transaction.WriteMap.Put(this,value);
        }

        internal override bool Validate(BaseVBoxBody readBody)
        {
            return _body == readBody;
        }

        internal override void Install(object value, int version)
        {
            _body = new VBoxBody<T>((T)value,version,_body);

            if (_listeners.Count == 0) return;

            var temp = _listeners;
            foreach (var retryLatch in temp)
            {
                retryLatch.Open(retryLatch.Era);
            }


            ImmutableList<IRetryLatch> initial;
            do
            {
                initial = _listeners;
            } while (initial != Interlocked.CompareExchange(ref _listeners, ImmutableList<IRetryLatch>.Empty, initial));
        }

        internal override void RegisterRetryLatch(IRetryLatch latch, BaseVBoxBody expectedBody, int expectedEra)
        {
            if (_body != expectedBody)
            {
                //if it currently already contains a different version, we are done.
                latch.Open(expectedEra);
                return;
            }

            while (true)
            {
                if (_body != expectedBody)
                {
                    //if it currently already contains a different version, we are done.
                    latch.Open(expectedEra);
                    return;
                }

                var initialValue = _listeners;
                var result = Interlocked.CompareExchange(ref _listeners, _listeners.Add(latch), initialValue);
                if (result != initialValue)
                {
                    continue;
                }

                if (_body == expectedBody)
                {
                    return;
                }
                //version has changed unregister
                while (true)
                {
                    initialValue = _listeners;
                    result = Interlocked.CompareExchange(ref _listeners, _listeners.Add(latch), initialValue);
                    if (result != initialValue)
                    {
                        continue;
                    }
                    return;
                }
            }
        }

        internal T ReadCommute()
        {
            return _body.Value;
        }

        public void Commute(JVTransaction transaction, Func<T, T> action)
        {
            transaction.Commutes.Add(new Commute<T>(action,this));
        }
    }
}
