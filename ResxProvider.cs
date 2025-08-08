﻿//************************************************************************************************
// Copyright © 2023 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace ResxTranslator
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Xml.Linq;


	internal static class ResxProvider
	{

		/// <summary>
		/// Gets a list of all translatable string resources
		/// </summary>
		/// <param name="root">An XElement of the root of a resx file</param>
		/// <returns>The list of data elements of all translatable strings</returns>
		public static List<XElement> CollectStrings(XElement root)
		{
			/*
			  <data name="DemoString" xml:space="preserve">
				<value>Foobar</value>
				<comment>NO</comment>
			  </data>
			*/

			// this should filter out all non-string entries and leave only strings
			return root.Elements("data")
				.Where(e =>
					// string entires always have xml:space= and do not have type=
					e.Attributes().Any(a => a.Name.LocalName == "space") &&
					e.Attribute("type") == null &&
					// TODO: what is this for?
					e.Attribute("name")?.Value.StartsWith(">>") != true &&
					// !SKIP is a special flag indicating this entry should not be translated
					e.Element("comment")?.Value.ContainsICIC("!SKIP") != true)
				.ToList();
		}


		/// <summary>
		/// Filters the data list by keeping only items that don't exist in the
		/// specified resx file. This finds all new items that need to be translated
		/// </summary>
		/// <param name="data"></param>
		/// <param name="path"></param>
		/// <returns></returns>
		public static List<XElement> CollectNewStrings(List<XElement> data, string path)
		{
			try
			{
				var target = XElement.Load(path);

				return data.Where(d =>
					// string entires always have xml:space= and do not have type=
					d.Attributes().Any(a => a.Name.LocalName == "space") &&
					d.Attribute("type") == null &&
					(
						// collect all edited entries
						d.Element("comment")?.Value.ContainsICIC("!EDIT") == true ||
						// collect entries that don't exist in target
						!target.Elements("data")
							.Any(e => e.Attribute("name")?.Value == d.Attribute("name").Value)
					))
					.ToList();
			}
			catch
			{
				// TODO: ...
			}

			return data;
		}



        /// <summary>
        /// Filters the data list by keeping only items that exists but are blank in
        /// specified resx file (path). 
        /// </summary>
        /// <param name="data">Neutral/english</param>
        /// <param name="path">Target translation</param>
        /// <returns></returns>
        public static List<XElement> CollectEmptyStrings(List<XElement> data, string path)
        {
            try
            {
                var target = XElement.Load(path);

                return data
                    .Where(e =>
                        e.Attributes().Any(a => a.Name.LocalName == "space") &&
                        e.Attribute("type") == null &&
                        // !SKIP is a special flag indicating this entry should not be translated
                        e.Element("comment")?.Value.ContainsICIC("!SKIP") != true &&

						// The translation is empty
                        string.IsNullOrEmpty(target.Elements("data")
                            .FirstOrDefault(d => d.Attribute("name")?.Value == e.Attribute("name").Value)?.Element("value")?.Value)
                    )
                    .ToList();
            }
            catch
            {
                // TODO: ...
            }

            return data;
        }


        /// <summary>
        /// Force merge hints from the specified file into the data collection.
        /// </summary>
        /// <param name="data">The data collection to modify</param>
        /// <param name="hintPath">The path of the hints file</param>
        /// <returns>Count of hints applied</returns>
        public static int MergeHints(XElement root, XElement hints)
		{
			var count = 0;
			foreach (var hint in hints.Elements())
			{
				var preferred = hint.Element("preferred").Value.Trim();

				var element = root.Elements("data").FirstOrDefault(e =>
					e.Attribute("name").Value == hint.Attribute("name").Value &&
					e.Element("value").Value != preferred);

				if (element != null)
				{
					// if found here then we did not find the translated value in the !EDIT
					// values but found the named item in the root so overwrite...

					element.Element("value").Value = preferred;
					count++;
				}
			}

			return count;
		}


		/// <summary>
		/// Sorts all data elements of a given resource file, prioritizing strings first,
		/// followed by files. Files are ordered by type, then folder path, then name.
		/// </summary>
		/// <param name="root">The root of the resx file as an XElement</param>
		public static void SortData(XElement root)
		{
			var data = root.Elements("data").ToList();
			data.ForEach(d => d.Remove());

			var strings = data
				.Where(e => e.Attribute("type") == null)
				.OrderBy(e => e.Attribute("name").Value)
				.ToList();

			if (strings.Any())
			{
				root.Add(strings);
			}

			// sort files by type, then directory path, then resource name...

			var files = data
				.Where(e => e.Attribute("type") != null)
				.Select(e => new { Element = e, Values = e.Element("value").Value.Split(';') })
				.OrderBy(e => e.Values[1])  // type
				.ThenBy(e => e.Values[0])   // path
				.ThenBy(e => e.Element.Attribute("name").Value)
				.Select(e => e.Element)
				.ToList();

			if (files.Any())
			{
				root.Add(files);
			}
		}
	}
}
