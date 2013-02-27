using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNP.Core
{
    public static class Extensions
    {
        
        public static T[] GetArraySection<T>(this T[] source, Int32 offset, Int32 length)
        {
            T[] _destinationArray = new T[length];
            Buffer.BlockCopy(source, offset, _destinationArray, 0, length);
            return _destinationArray;
        }

        public static T[] Merge<T>(this T[] source, T[] arrayToMerge)
        {
            T[] _destinationArray = new T[source.Length + arrayToMerge.Length];
            Buffer.BlockCopy(source, 0, _destinationArray, 0, source.Length);
            Buffer.BlockCopy(arrayToMerge, 0, _destinationArray, source.Length, arrayToMerge.Length);
            return _destinationArray;
        }

        public static T Execute<T>(Func<T> func, int timeout)
        {
            T result;
            TryExecute(func, timeout, out result);
            return result;
        }

        public static bool TryExecute<T>(Func<T> func, int timeout, out T result)
        {
            T t = default(T);

            Thread thread = new Thread(() => t = func());
            thread.Start();

            var completed = thread.Join(timeout);

            if (!completed)
            {
                thread.Abort();
            }

            result = t;
            return completed;
        }

        public static T Coalesce<T>(params T[] args) where T : class
        {
            if (args == null)
            {
                throw new ArgumentException("The value supplied cannot be null", "args");
            }
            for (Int16 i = 0; i < args.Length; i++)
            {
                if (args[i] != null)
                {
                    return args[i];
                }
            }
            return default(T);
        }

        /// <summary>
        /// Fills an array with a value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="value"></param>
        public static void MemSet<T>(T[] array, T value)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            int block = 32, index = 0;
            int length = Math.Min(block, array.Length);

            //Fill the initial array
            while (index < length)
            {
                array[index++] = value;
            }

            length = array.Length;
            while (index < length)
            {
                Buffer.BlockCopy(array, 0, array, index, Math.Min(block, length - index));
                index += block;
                block *= 2;
            }
        }
    }
}
