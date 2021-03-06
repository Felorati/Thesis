﻿using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Evaluation.Common;
using STM.Implementation.Lockbased;
using STM.Collections;

namespace Evaluation.Library.SantaClausImpl
{
    public class Reindeer : IStartable
    {
        private readonly Random _randomGen = new Random(Guid.NewGuid().GetHashCode());
        public int ID { get; private set; }

        private readonly Queue<Reindeer> _reindeerBuffer;
        private readonly TMVar<bool> _workingForSanta = new TMVar<bool>(false);
        private readonly TMVar<bool> _waitingAtSleigh = new TMVar<bool>(false);
        private readonly TMVar<bool> _waitingInHut = new TMVar<bool>(false);

        public Reindeer(int id, Queue<Reindeer> buffer)
        {
            ID = id;
            _reindeerBuffer = buffer;
        }
        
        public Task Start()
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(100 * _randomGen.Next(10));

                    STMSystem.Atomic(() =>
                    {
                        _reindeerBuffer.Enqueue(this);
                        _waitingInHut.Value = true;
                    });

                    Console.WriteLine("Reindeer {0} is back",ID);

                    //Waiting in the warming hut
                    STMSystem.Atomic(() =>
                    {
                        if (_waitingInHut)
                        {
                            STMSystem.Retry();
                        }
                    });

                    //Wait for santa to be ready
                    STMSystem.Atomic(() =>
                    {
                        if (_waitingAtSleigh)
                        {
                            STMSystem.Retry();
                        }
                    });

                    //Delivering presents

                    //Wait to be released by santa
                    STMSystem.Atomic(() =>
                    {
                        if (_workingForSanta)
                        {
                            STMSystem.Retry();
                        }
                    });   
                }
            });
        }

        public void CallToSleigh()
        {
            STMSystem.Atomic(() =>
            {
                _waitingInHut.Value = false;
                _waitingAtSleigh.Value = true;
            });
        }

        public void HelpDeliverPresents()
        {
            STMSystem.Atomic(() =>
            {
                _waitingAtSleigh.Value = false;
                _workingForSanta.Value = true;
            });
        }

        public void ReleaseReindeer()
        {
            _workingForSanta.Value = false;
        }
    }
}
