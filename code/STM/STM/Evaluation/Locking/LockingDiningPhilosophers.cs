﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Evaluation.Locking
{
    public class LockingDiningPhilosophers
    {
        private const int MAX_EAT_COUNT = 1000;

        public static void Start()
        {
            var eatCounter = new LockCounter();
            var fork1 = new object();
            var fork2 = new object();
            var fork3 = new object();
            var fork4 = new object();
            var fork5 = new object();

            var t1 = StartPhilosopher(eatCounter, fork1, fork2);
            var t2 = StartPhilosopher(eatCounter, fork2, fork3);
            var t3 = StartPhilosopher(eatCounter, fork3, fork4);
            var t4 = StartPhilosopher(eatCounter, fork4, fork5);
            var t5 = StartPhilosopher(eatCounter, fork5, fork1);

            Task.WaitAll(t1, t2, t3, t4, t5);
        }

        private static Task StartPhilosopher(LockCounter eatCounter, object left, object right)
        {
            var t1 = new Task(() =>
            {
                while (eatCounter.Get() < MAX_EAT_COUNT)
                {
                    lock (left)
                    {
                        if (Monitor.TryEnter(right, 100))
                        {
                            try
                            {
                                Console.WriteLine("Thread: " + Thread.CurrentThread.ManagedThreadId + " eating.");
                                Thread.Sleep(100);
                                Console.WriteLine("Eat count: " + eatCounter.IncrementAndGet());
                            }
                            finally
                            {
                                Monitor.Exit(right);
                            }
                        }
                    }

                    Thread.Sleep(100);
                }
            });

            t1.Start();

            return t1;
        }

        private class LockCounter
        {
            protected int Count = 0;
            protected readonly object LockObject = new object();

            public int IncrementAndGet()
            {
                int tmp = 0;
                lock (LockObject)
                {
                    tmp = ++Count;
                }

                return tmp;
            }

            public void Increment()
            {
                lock (LockObject)
                {
                    ++Count;
                }
            }

            public int Get()
            {
                int tmp = 0;
                lock (LockObject)
                {
                    tmp = Count;
                }

                return tmp;
            }

        }
    }
}