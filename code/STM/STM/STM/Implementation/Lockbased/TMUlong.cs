﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STM.Interfaces;

namespace STM.Implementation.Lockbased
{
    public class TMUlong : TMVar<ulong>, Incrementable
    {
        public TMUlong()
        {

        }

        public TMUlong(ulong l)
             : base(l)
        {

        }

         #region Inc Dec
         public void Inc()
         {
             this.Value = this.Value + 1;
         }

         public void Dec()
         {
             this.Value = this.Value - 1;
         }

         public static TMUlong operator ++(TMUlong tmLong)
         {
             tmLong.Inc();
             return tmLong;
         }

         public static TMUlong operator --(TMUlong tmLong)
         {
             tmLong.Dec();
             return tmLong;
         }

         #endregion Inc Dec
    }
}