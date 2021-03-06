﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using STM;
using STM.Collections;
using STM.Interfaces;
using STM.Implementation.Obstructionfree;
using STM.Implementation.Lockbased;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using STM.Implementation.JVSTM;

namespace STMTester
{
    class Program
    {
        private const int MAX_EAT_COUNT = 1000;


        static void Main(string[] args)
        {
            //Test1();
            //Test2();
            //Test3();
            //Test4();
            //TestRetry();
            //TestRetry2();
            //SingleItemBufferTest();
            //QueueTest();
            //AtomicLockTest();
            //DinningPhilosophersTest();
            //OrElseNestingTest();
            //OrElseTest();
            //OrElseNestingTest2();
            //OrElseNestingTest3();
            //var dining = new DiningPhilosopher();
            //dining.Start();
            //TestMSQueue();

            //JVSpeedTest();
            //JVSpeedTest();

            //var dinning = new JVDining();
            //dinning.Start();

            //JVTest();
            //JVConcurrentTest();
            /*
            for (int j = 0; j < 100; j++)
            {
                var result = new VBox<int>(0);

                var t1 = new Thread(() =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        JVSTMSystem.Atomic((transaction) =>
                        {
                            result.Put(transaction, result.Read(transaction) + 1);
                        });
                    }


                });

                var t2 = new Thread(() =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        JVSTMSystem.Atomic((transaction) =>
                        {
                            result.Put(transaction, result.Read(transaction) + 1);
                        });
                    }

                });

                t1.Start();
                t2.Start();

                t1.Join();
                t2.Join();

                var res = JVSTMSystem.Atomic((transaction) => result.Read(transaction));
                if (res != 2000)
                {
                    Console.WriteLine("Error: " + res);
                }
            }*/

            JVSTMSystem.StartGC();
            var result = new VBox<int>(0);

            var t1 = new Thread(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    JVSTMSystem.Atomic((transaction) =>
                    {
                        result.Put(transaction, result.Read(transaction) + 1);
                    });
                }


            });

            var t2 = new Thread(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    JVSTMSystem.Atomic((transaction) =>
                    {
                        result.Put(transaction, result.Read(transaction) + 1);
                    });
                }

            });

            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();
            Thread.Sleep(10);
            Console.WriteLine(result.GetNrBodies());
            Console.WriteLine("Done");
            Console.ReadKey();
        }

        private static void JVSpeedTest()
        {
            var b = new VBox<bool>();
            var tmb = new TMVar<bool>();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                bool notCommitted = true;
                while (notCommitted)
                {
                    var t = JVTransaction.Start();
                    b.Put(t, !b.Read(t));
                    notCommitted = !t.Commit();
                }
            }

            sw.Stop();

            Console.WriteLine("Non system time: " + sw.ElapsedMilliseconds);


            sw = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                JVSTMSystem.Atomic((t) =>
                {
                    if (b.Read(t))
                    {
                        b.Put(t, false);
                    }
                    else
                    {
                        b.Put(t, true);
                    }
                });
            }

            sw.Stop();

            Console.WriteLine("System time: " + sw.ElapsedMilliseconds);


            sw = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                STMSystem.Atomic(() =>
                {
                    if (tmb.Value)
                    {
                        tmb.Value = false;
                    }
                    else
                    {
                        tmb.Value = true;
                    }
                });
            }
            sw.Stop();

            Console.WriteLine("TL2 time: " + sw.ElapsedMilliseconds);
        }


        private static void JVTest()
        {
            var box1 = new VBox<string>("a");
            var box2 = new VBox<bool>(false);
            var notCommited = true;
            while (notCommited)
            {
                var transaction = JVTransaction.Start();
                box1.Put(transaction, "Hello world");
                var b2Value = box2.Read(transaction);
                notCommited = !transaction.Commit();
            }
        }

        private static void JVConcurrentTest()
        {
            for (int i = 0; i < 10000; i++)
            {
                var res = JVConcurrentTestInternal();
                Console.WriteLine(res);
                if (res == 120)
                {
                    throw new Exception("Res == 120");
                }
            }
        }

        private static int JVConcurrentTestInternal()
        {
            var box = new VBox<int>(10);

            var t1 = new Thread(() =>
            {
                var notCommited = true;
                while (notCommited)
                {
                    var transaction = JVTransaction.Start();
                    if (box.Read(transaction) == 10)
                    {
                        box.Put(transaction, box.Read(transaction) * 10);
                    }
                    else
                    {
                        box.Put(transaction,5);
                    }
                    notCommited = !transaction.Commit();
                }
            });

            var t2 = new Thread(() =>
            {
                var notCommited = true;
                while (notCommited)
                {
                    var transaction = JVTransaction.Start();
                    box.Put(transaction, 12);
                    notCommited = !transaction.Commit();
                }
                
            });

            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();
            var t = JVTransaction.Start();
            var result = box.Read(t);
            t.Commit();

            return result;
        }

        private static void DinningPhilosophersTest()
        {
            var eatCounter = new LockCounter();
            var fork1 = new TMVar<bool>(true);
            var fork2 = new TMVar<bool>(true);
            var fork3 = new TMVar<bool>(true);
            var fork4 = new TMVar<bool>(true);
            var fork5 = new TMVar<bool>(true);

            var t1 = StartPhilosopher(eatCounter, fork1, fork2);
            var t2 = StartPhilosopher(eatCounter, fork2, fork3);
            var t3 = StartPhilosopher(eatCounter, fork3, fork4);
            var t4 = StartPhilosopher(eatCounter, fork4, fork5);
            var t5 = StartPhilosopher(eatCounter, fork5, fork1);

            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();
            t5.Join();
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
        private static Thread StartPhilosopher(LockCounter eatCounter, TMVar<bool> left, TMVar<bool> right)
        {
            var t1 = new Thread(() =>
            {
                while (eatCounter.Get() < MAX_EAT_COUNT)
                {
                    STMSystem.Atomic(() =>
                    {
                        if (!left|| !right)
                        {
                            STMSystem.Retry();
                        }

                        left.Value = false;
                        right.Value = false;
                    });

                    Console.WriteLine("Thread: " + Thread.CurrentThread.ManagedThreadId + " eating.");
                    Thread.Sleep(100);
                    Console.WriteLine("Eat count: "+eatCounter.IncrementAndGet()); 


                    STMSystem.Atomic(() =>
                    {
                        left.Value = true;
                        right.Value = true;
                    });

                    Thread.Sleep(100);
                }
            });

            t1.Start();

            return t1;
        }

        private static void QueueTest()
        {
            Console.WriteLine("QueueTest:");
            var buffer = new STM.Collections.Queue<int>();
            var t1 = new Thread(() =>
            {
                var sw = Stopwatch.StartNew();
                for (var i = 0; i < 1000; i++)
                {
                    Console.WriteLine(buffer.Dequeue());
                    Console.WriteLine("Index :"+i);
                }

                sw.Stop();
                Console.WriteLine("Milisecs: "+sw.ElapsedMilliseconds);
            });

            var t2 = new Thread(() => 
            {
                for (var i = 0; i < 1000; i++)
                {
                    buffer.Enqueue(i);
                }
            });
            
            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();
        }



        private static void TestRetry2()
        {
            Console.WriteLine("Retry as only operation:");
            var t1 = new Thread(() =>
            {
                STMSystem.Atomic(STMSystem.Retry);
            });

            t1.Start();
            t1.Join();
        }



        private static void Test4()
        {
            for (int i = 0  ; i < 10000; i++)
            {
                var result = Test4Internal();
                Console.WriteLine("Result: " + result);
                Debug.Assert(result != 120);
            }
        }

        private static int Test4Internal()
        {
            var result = new TMVar<ValueHolder>(new ValueHolder(10));

            var t1 = new Thread(() =>
            {
                var r1 = STMSystem.Atomic(() =>
                {
                    if (result.GetValue().Value == 10)
                    {
                        Thread.Yield();
                        result.SetValue(new ValueHolder(result.GetValue().Value * 10));
                    }

                    return result.GetValue();
                });
                Debug.Assert(r1.Value != 120, "String value: "+r1.Value);
            });

            var t2 = new Thread(() => STMSystem.Atomic(() => result.SetValue(new ValueHolder(12))));

            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();

            return result.GetValue().Value;
        }

        private static void Test3()
        {
            STMObject<ValueHolder> result = new FreeObject<ValueHolder>(new ValueHolder(10));
            var system = FreeStmSystem.GetInstance();

            var t1 = new Thread(() =>
            {
                var r1 = system.Atomic(() =>
                {
                    if (result.GetValue().Value == 10)
                    {
                        Thread.Yield();
                        result.SetValue(new ValueHolder(0));
                    }
                    else
                    {
                        result.SetValue(new ValueHolder(1));
                    }
                    return result.GetValue();
                });
                Console.WriteLine("Result: "+r1);
            });

            var t2 = new Thread(() => system.Atomic(() => result.SetValue(new ValueHolder(12))));

            t1.Start();
            t2.Start();
            
            t1.Join();
            t2.Join();
        }

        private static void Test2()
        {
            var system = FreeStmSystem.GetInstance();
            STMObject<ValueHolder> fo = new FreeObject<ValueHolder>(new ValueHolder(5));
            var result = system.Atomic(() =>
            {
                fo.SetValue(new ValueHolder(12));
                return fo.GetValue();
            });

        }

        private static void Test1()
        {
            var system = FreeStmSystem.GetInstance();
            STMObject<ValueHolder> fo = new FreeObject<ValueHolder>(new ValueHolder(15));

            var t1 = new Thread(() =>
            {
                var result = system.Atomic(() =>
                {
                    fo.SetValue(new ValueHolder(1));
                    return fo.GetValue();
                });
            });

            var t2 = new Thread(() =>
            {
                var result = system.Atomic(() =>
                {
                    fo.SetValue(new ValueHolder(2));
                    return fo.GetValue();
                });
            });

            var t3 = new Thread(() =>
            {
                var result = system.Atomic(() =>
                {
                    fo.SetValue(new ValueHolder(3));
                    return fo.GetValue();
                });
            });

            var t4 = new Thread(() =>
            {
                var result = system.Atomic(() =>
                {
                    fo.SetValue(new ValueHolder(4));
                    return fo.GetValue();
                });
            });

            var t5 = new Thread(() =>
            {
                var result = system.Atomic(() =>
                {
                    fo.SetValue(new ValueHolder(5));
                    return fo.GetValue();
                });
            });

            t1.Start();
            t2.Start();
            t3.Start();
            t4.Start();
            t5.Start();

            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();
            t5.Join();
        }

        private class ValueHolder : ICopyable<ValueHolder>
        {
            public int Value { get; set; }

            public ValueHolder()
            {
                Value = default(int);
            }

            public ValueHolder(int value)
            {
                Value = value;
            }

            public void CopyTo(ValueHolder other)
            {
                other.Value = Value;
            }

            public override string ToString()
            {
                return Value.ToString();
            }
        }
    }
}
