﻿using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable CheckNamespace

namespace Octopus.Shared.Util
{
    public static class StringExtensions
    // ReSharper restore CheckNamespace
    {
        public static string FirstLineTrimmedTo(this string text, int length)
        {
            text = text ?? string.Empty;
            text = text.Split('\n').First();
            return text.LimitWithEllipses(length);
        }

        /// <summary>
        /// Substring but OK if shorter
        /// </summary>
        public static string Limit(this string str, int characterCount)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            if (str.Trim().Length <= characterCount) return str.Trim();
            else return str.Substring(0, characterCount).Trim();
        }

        /// <summary>
        /// Substring with elipses but OK if shorter, will take 3 characters off character count if necessary
        /// </summary>
        public static string LimitWithEllipses(this string str, int characterCount)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            if (characterCount < 5) return str.Limit(characterCount);       // Can't do much with such a short limit
            if (str.Trim().Length <= characterCount) return str.Trim();
            else return str.Substring(0, characterCount - 3) + "...";
        }

        public static string AndList(this string[] items)
        {
            if (items.Length == 1) return items[0];
            return string.Join(", ", items.Take(items.Length - 1)) + " and " + items.Last();
        }

        public static string NewLineSeperate(this IEnumerable<object> items) => string.Join(Environment.NewLine, items);

        public static string ConstructAngularFriendlyQueryString(this System.Collections.Specialized.NameValueCollection @params)
        {
            var items = new List<string>();
            foreach (string key in @params)
            {
                // Needs to be angular friendly (eg. "?projects=Project-01&projects=Project-02" ... not "?projects=Project-01,Projects-02").
                var values = @params[key].Split(',');
                foreach (var value in values)
                {
                    items.Add(string.Concat(key, "=", System.Web.HttpUtility.UrlEncode(value)));
                }
            }
            return string.Join("&", items.ToArray());
        }
    }
}