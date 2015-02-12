﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STM.Implementation.Lockbased
{
    public class LockObject<T> : BaseLockObject
    {
        private T version;

        public LockObject(T value)
        {
            version = value;
        }

        public virtual void SetValue(T value)
        {
            version = value;
        }

        public virtual T GetValue()
        {
            T tmp;
            Lock();
            tmp = version;
            Unlock();
            
            return tmp;
        }

        public virtual bool Validate()
        {
            Transaction me = Transaction.GetLocal();
            switch (me.GetStatus())
            {
                case Transaction.Status.Committed:
                    return true;
                case Transaction.Status.Active:
                    return GetStamp() <= VersionClock.GetReadStamp();
                case Transaction.Status.Aborted:
                    return false;
                default:
                    throw new Exception("Shits on fire yo!");
            }
        }

        public override void SetValueCommit(object o)
        {

#if DEBUG
            Transaction me = Transaction.GetLocal();
            Console.WriteLine("Transaction: " + me.ID + " commited:" + o);
#endif
            this.version = (T)o;
        }
    }
}
