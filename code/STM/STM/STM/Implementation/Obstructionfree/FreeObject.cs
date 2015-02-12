﻿using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using Spring.Threading.AtomicTypes;
using STM.Exceptions;
using STM.Interfaces;
using ThreadState = System.Threading.ThreadState;

namespace STM.Implementation.Obstructionfree
{
    public class FreeObject<T> : STMObject<T> where T : class, ICopyable<T>, new()
    {
        private readonly AtomicReference<Locator> _start;
        public FreeObject(T value) : base(value)
        {
            _start = new AtomicReference<Locator>(new Locator(Transaction.GetLocal(),value,value));
        }

        private class Locator
        {
            public Transaction Owner;
            public T NewValue;
            public T OldValue;

            public Locator()
            {
                
            }

            public Locator(Transaction owner, T newValue, T oldValue)
            {
                Owner = owner;
                NewValue = newValue;
                OldValue = oldValue;
            }
        }

        protected override T OpenRead()
        {
            var me = Transaction.GetLocal();
            switch (me.GetStatus())
            {
                case Transaction.Status.Committed:
                    return base.GetValue();
                case Transaction.Status.Aborted:
                    throw new STMException("Open write from aborted transaction");
                case Transaction.Status.Active:
                    var locator = _start.Value;
                    if (locator.Owner == me)
                        return locator.NewValue;

                    var newLocator = new Locator();
                    while (Thread.CurrentThread.ThreadState != ThreadState.AbortRequested)
                    {
                        var oldLocator = _start.Value;
                        var owner = oldLocator.Owner;
                        switch (owner.GetStatus())
                        {
                            case Transaction.Status.Committed:
                            case Transaction.Status.Aborted:
                                break;
                            case Transaction.Status.Active:
                                //Contention manager here
                                me.Abort();
                                break;
                        }

                        newLocator.NewValue = (T)Activator.CreateInstance(this.Type);
                        newLocator.OldValue = (T)Activator.CreateInstance(this.Type);
                        oldLocator.NewValue.CopyTo(newLocator.NewValue);
                        oldLocator.OldValue.CopyTo(newLocator.OldValue);
                        newLocator.Owner = me;
                        if (_start.CompareAndSet(oldLocator, newLocator))
                            return newLocator.NewValue;
                    }
                    me.Abort();
                    throw new STMException("Open write from aborted transaction");
                default:
                    throw new Exception("STM system error");
            }
        }

        public override T GetValue()
        {
            return OpenRead();
        }

        public override void SetValue(T newValue)
        {
            var me = Transaction.GetLocal();
            switch (me.GetStatus())
            {
                case Transaction.Status.Committed:
                    base.SetValue(newValue);
                    return;
                case Transaction.Status.Aborted:
                    throw new STMException("Open write from aborted transaction");
                case Transaction.Status.Active:
                    var locator = _start.Value;
                    if (locator.Owner == me)
                    {
                        newValue.CopyTo(locator.NewValue);
                        return;
                    }

                    var newLocator = new Locator();
                    while (Thread.CurrentThread.ThreadState != ThreadState.AbortRequested)
                    {
                        var oldLocator = _start.Value;
                        var owner = oldLocator.Owner;
                        switch (owner.GetStatus())
                        {
                            case Transaction.Status.Committed:
                                newLocator.OldValue = oldLocator.NewValue;
                                break;
                            case Transaction.Status.Aborted:
                                newLocator.OldValue = oldLocator.OldValue;
                                break;
                            case Transaction.Status.Active:
                                //Contention manager here
                                me.Abort();
                                throw new STMException("Open write from aborted transaction");
                        }

                        newLocator.NewValue = newValue;
                        //newLocator.OldValue.CopyTo(newLocator.NewValue);
                        newLocator.Owner = me;
                        if (_start.CompareAndSet(oldLocator, newLocator))
                            Console.WriteLine("New: " + newLocator.NewValue);
                            Console.WriteLine("Old: " + newLocator.OldValue);
                            return;
                    }
                    me.Abort();
                    throw new STMException("Open write from aborted transaction");
                default:
                    throw new Exception("STM system error");
            }
        }

        protected override T OpenWrite()
        {
            var me = Transaction.GetLocal();
            switch (me.GetStatus())
            {
                case Transaction.Status.Committed:
                    return base.GetValue();
                case Transaction.Status.Aborted:
                    throw new STMException("Open write from aborted transaction");
                    case Transaction.Status.Active:
                    var locator = _start.Value;
                    if (locator.Owner == me)
                        return locator.NewValue;

                    var newLocator = new Locator();
                    while (Thread.CurrentThread.ThreadState != ThreadState.AbortRequested)
                    {
                        var oldLocator = _start.Value;
                        var owner = oldLocator.Owner;
                        switch (owner.GetStatus())
                        {
                            case Transaction.Status.Committed:
                                newLocator.OldValue = oldLocator.NewValue;
                                break;
                            case Transaction.Status.Aborted:
                                newLocator.OldValue = oldLocator.OldValue;
                                break;
                            case Transaction.Status.Active:
                                //Contention manager here
                                me.Abort();
                                throw new STMException("Open write from aborted transaction");
                        }
                        
                        newLocator.NewValue = (T)Activator.CreateInstance(this.Type);
                        newLocator.OldValue.CopyTo(newLocator.NewValue);
                        newLocator.Owner = me;
                        if (_start.CompareAndSet(oldLocator, newLocator))
                            return newLocator.NewValue;
                    }
                    me.Abort();
                    throw new STMException("Open write from aborted transaction");
                default:
                    throw new Exception("STM system error");
            }
        }

        public override bool Validate()
        {
            var me = Transaction.GetLocal();
            return me.GetStatus() == Transaction.Status.Active;
        }

        
    }
}