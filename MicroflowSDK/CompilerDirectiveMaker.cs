using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroflowSDK
{
    public class CompilerDirectiveMaker
    {
        public static string MakeCompilerDirectiveString(string largestCombo)
        {
            var arr = largestCombo.Split('_');

            var combinations = CreateCombinations(0, "", arr);
            var list = new List<string>();


            for (int i1 = 1; i1 < arr.Length; i1++)
            {
                string i = arr[i1];

                for (int yy = 1; yy < combinations.Count; yy++)
                {
                    string j = combinations[yy];

                    if (!j.StartsWith(i))
                    {
                        var idx = j.IndexOf(i);
                        if (idx > -1)
                        {
                            combinations[yy] = j.Insert(idx, "_");
                        }
                    }
                }
            }

            for (int i1 = 0; i1 < combinations.Count; i1++)
            {
                list.Add("RELEASE_NO_" + combinations[i1]);
                list.Add("DEBUG_NO_" + combinations[i1]);
            }

            list.Add("DEBUG");
            list.Add("RELEASE");

            //var tuple = GetCSharpDirectiveForOption()
            list.Sort();

            return $"{string.Join(";", list)}";
        }

        private static List<string> CreateCombinations(int startIndex, string pair, string[] initialArray)
        {
            var combinations = new List<string>();
            for (int i = startIndex; i < initialArray.Length; i++)
            {
                var value = $"{pair}{initialArray[i]}";
                combinations.Add(value);
                combinations.AddRange(CreateCombinations(i + 1, value, initialArray));
            }

            return combinations;
        }

        public static string GetCompilerDirectiveForOptionToExclude(bool IsDebug, string key, string config)
        {
            var li = config.Split(';').ToList();
            
            string res = $"#if DEBUG || RELEASE || !{string.Join(" && !", li.FindAll(r => r.Contains(key)))}";

            return res;
        }
    }
}
