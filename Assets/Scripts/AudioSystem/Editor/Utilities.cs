using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace MordiAudio
{
    namespace Utility
    {
        public static class Extensions
        {
            #region String

            public static bool LastCharacterIsNumber(this string str) {
                string lastChar = str.Substring(str.Length - 1);
                return int.TryParse(lastChar, out _);
            }

            /// <summary>
            /// First, numbers are trimmed off of the end. Then, whitespace is trimmed.
            /// </summary>
            /// <param name="str">String to be trimmed.</param>
            /// <returns>Trimmed string. If string consists only of numbers/whitespace, an empty string is returned.</returns>
            public static string TrimTrailingNumbersAndWhitespace(this string str) {
                while (str.LastCharacterIsNumber()) {
                    str = str.Substring(0, str.Length - 1);
                }
                // Trim whitespace
                return str.Trim();
            }

            /// <summary>
            /// Get friendly name of this type.
            /// </summary>
            /// <param name="type">Type</param>
            /// <returns>Friendly name of type (eg; List<String></returns>
            public static string GetFriendlyName(this Type type) {
                string friendlyName = type.Name;
                if (type.IsGenericType) {
                    int iBacktick = friendlyName.IndexOf('`');
                    if (iBacktick > 0) {
                        friendlyName = friendlyName.Remove(iBacktick);
                    }
                    friendlyName += "<";
                    Type[] typeParameters = type.GetGenericArguments();
                    for (int i = 0; i < typeParameters.Length; ++i) {
                        string typeParamName = GetFriendlyName(typeParameters[i]);
                        friendlyName += (i == 0 ? typeParamName : "," + typeParamName);
                    }
                    friendlyName += ">";
                }

                return friendlyName;
            }

            #endregion
        }

        public static class Editor
        {
            public static string[] FindAssetPaths(string filter) {
                string[] assets = AssetDatabase.FindAssets(filter);
                for (int i = 0; i < assets.Length; i++) {
                    assets[i] = AssetDatabase.GUIDToAssetPath(assets[i]);
                }
                return assets;
            }

            /// <summary>
            /// Filters and loads assets of type.
            /// </summary>
            /// <typeparam name="T">Type (must be UnityEngine Object)</typeparam>
            /// <param name="filter">Filter string, excluding type name.</param>
            /// <returns></returns>
            public static T[] FindAndLoadAssets<T>(string filter) where T: UnityEngine.Object {
                string[] assetPaths = FindAssetPaths($"t:{typeof(T).GetFriendlyName()} {filter}");
                T[] assets = new T[assetPaths.Length];
                for (int i = 0; i < assetPaths.Length; i++) {
                    assets[i] = AssetDatabase.LoadAssetAtPath<T>(assetPaths[i]);
                }
                return assets;
            }
        }
    }
}