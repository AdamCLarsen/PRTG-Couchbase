using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using NUnit.Framework;

namespace Prtg.Sensor.Couchbase.Tests
{
	public class ScrapBucketAnalyticsHtml
	{
		[Test]
		public void Run()
		{
			var doc = new HtmlDocument();
			doc.OptionOutputOriginalCase = true;
			doc.Load("BucketAnalytics.html");
			var root = doc.DocumentNode;
			var targetNode = root.SelectSingleNode("//div[@id='js_stats_container']");
			foreach (var section in targetNode.SelectNodes(".//div[contains(@class, 'darker_block')]"))
			{
                var header = section.SelectSingleNode("h3");
				// Console.WriteLine("-----------");
				 Console.WriteLine("// " + header.InnerText);
				foreach (var selectNode in section.SelectNodes(".//div[contains(@class, 'analytics-small-graph')]"))
				{
					var dataGraph = selectNode.Attributes["data-graph"].Value;
					var labelSpan = selectNode.SelectSingleNode(".//span[@class='label-text']");
					Console.WriteLine("\"" + dataGraph.Trim() + "\": {\"Name\": \"" + labelSpan.InnerText.Trim() + "\" },");
				}
			}
		}
	}
}
