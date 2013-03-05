using System;
using System.ComponentModel.Composition;
using MNP.Core;
using MNP.Core.Enums;

namespace MNP.PluginSample
{
#warning "Disabled the line below as cannot export generic interface through MEF"
    //[Export(typeof(IObservableQueue<T>))]
    public sealed class ObservableQueueSample<T> : IObservableQueue<T>
    {
        public void Enqueue(T value, bool notifySubscribers)
        {
            throw new NotImplementedException();
        }

        public T Dequeue()
        {
            throw new NotImplementedException();
        }

        public T PeekOrDefault()
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { throw new NotImplementedException(); }
        }

        public void ChangeState(string id, QueuedProcessState newState)
        {
            throw new NotImplementedException();
        }

        public void Remove(Predicate<T> criteria)
        {
            throw new NotImplementedException();
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe(IObserver<T> observer)
        {
            throw new NotImplementedException();
        }
    }
}
