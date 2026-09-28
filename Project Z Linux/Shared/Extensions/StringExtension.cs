using System.Collections.Generic;

namespace ProjectZ.Shared.Extensions {
    static class StringExtension {

        /// <summary>
        /// Searches for a string within another string.
        /// </summary>
        /// <param name="Input">The String Containing the String You are Searching for.</param>
        /// <param name="SearchFor"></param>
        /// <param name="Startindex">Start Index Modifier.</param>
        /// <returns>Indexies in an array.</returns>
        /// <remarks></remarks>
        public static int[] Search(this string Input, string SearchFor, int Startindex) {
            var Indexies = new List<int>() { Input.IndexOf(SearchFor, Startindex) };
            int textEnd = Input.Length;
            int index = Startindex;
            int lastIndex = Input.LastIndexOf(SearchFor);
            while (index < lastIndex) {
                index = Input.IndexOf(SearchFor, index) + 1;
                Indexies.Add(index);
            }
            return Indexies.ToArray();
        }
    }

}