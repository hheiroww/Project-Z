using System;

namespace ProjectZ.Shared.Extensions {

    public static class GenericExtension {

        public static void Add<T>(ref T[] arr, T item) {
            Array.Resize(ref arr, arr.Length + 1);
            arr[arr.Length - 1] = item;
        }

    }

}