﻿using System;
using System.ComponentModel.Composition;
using MNP.Core;

namespace MNP.PluginSample
{
#warning "Disabled the line below as cannot export generic interface through MEF"
    //[Export(typeof(IManagedResource<T>))]
    public sealed class ManagedResourceSample<T> : IManagedResource<T>
    {
        public int Capacity
        {
            get { throw new NotImplementedException(); }
        }

        public void Insert(T item)
        {
            throw new NotImplementedException();
        }

        public T TakeNext()
        {
            throw new NotImplementedException();
        }
    }
}
