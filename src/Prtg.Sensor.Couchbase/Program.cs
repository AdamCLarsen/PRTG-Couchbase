using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Prtg.Sensor.Couchbase.Config;

namespace Prtg.Sensor.Couchbase
{
	class Program
	{
		private static StatsMappings _mappings;
		private static void Main(string[] args)
		{
			try
			{
				var location = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(Program)).Location);
				_mappings = JsonConvert.DeserializeObject<StatsMappings>(File.ReadAllText(Path.Combine(location, "CouchbaseStatsMap.json")));

				var result = Read(new RequestPrams
				{
					ServerAndPort = "localhost:8091",
					BucketName = "beer-sample",
					User = "Administrator",
					Password = "password"
				}).Result;

				result.WriteToConsole();
			}
			catch (Exception ex)
			{
				var baseException = ex.GetBaseException();
				new Prtg { Error = 1, Text = baseException.Message }.WriteToConsole();
			}

			if (Debugger.IsAttached)
			{
				Console.WriteLine("Press any key to continue...");
				Console.ReadKey();
			}
		}

		/// <exception cref="UriFormatException">NoteIn the .NET for Windows Store apps or the Portable Class Library, catch the base class exception, <see cref="T:System.FormatException" />, instead.The length of <paramref name="stringToEscape" /> exceeds 32766 characters.</exception>
		/// <exception cref="EncoderFallbackException">A fallback occurred (see Character Encoding in the .NET Framework for complete explanation)-and-<see cref="P:System.Text.Encoding.EncoderFallback" /> is set to <see cref="T:System.Text.EncoderExceptionFallback" />.</exception>
		/// <exception cref="ArgumentNullException">Value is null. </exception>
		/// <exception cref="ArgumentException">Stream does not support reading. </exception>
		/// <exception cref="HttpRequestException"></exception>
		public static async Task<Prtg> Read(RequestPrams request)
		{
			var client = new HttpClient
			{
				// http://localhost:8091/pools/default/buckets/TestClient_account/stats?zoom=minute
				BaseAddress = new Uri("http://" + request.ServerAndPort + "/pools/default/buckets/")
			};

			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(request.User + ":" + request.Password)));
			using (var result = await client.GetAsync(Uri.EscapeUriString(request.BucketName) + @"/stats?zoom=minute"))
			{
				result.EnsureSuccessStatusCode();
				using (var s = await result.Content.ReadAsStreamAsync())
				{
					var reader = new JsonTextReader(new StreamReader(s));
					return new Prtg { Results = (ReadData(JToken.Load(reader)).ToList()) };
				}
			}
		}

		private static IEnumerable<PrtgResult> ReadData(JToken token)
		{
			var sampleToken = token.SelectToken(@"op.samples", true);
			using (var s = File.Create("output.txt"))
			using (var w = new StreamWriter(s))
			{
				foreach (var child in sampleToken.Children())
				{
					var property = child as JProperty;
					if (property == null || !property.HasValues)
						// not sure what's going on here, but let's not crash.
						continue;

					// Check if this is a node we are interested in

					// Create the PRTG result for this record.
					// Have to do some de-duping because I have seen Couchbase return the same node more then once.
					StatsMappings.Channel mapping;
					if (!_mappings.Channels.TryGetValue(property.Name, out mapping))
					{
						// We don't have a mapping, so we will skip this value.
						continue;
					}

					// Calculate the current value, Couchbase by default captures every second, 
					//  and we will assume here that Zoom is at to minute
					//
					// We have 4 ways we can return the values
					//  - Average value over the last minute (Default)
					//  - Minimum value over the last minute
					//  - Maximum value over the last minute
					//  - The most recent value (not yet how this set is ordered)
					var count = 0;
					var sum = 0D;

					foreach (var value in property.Values())
					{
						switch (value.Type)
						{
							case JTokenType.Integer:
								sum += value.Value<long>();
								count++;
								break;
							case JTokenType.Float:
								sum += value.Value<double>();
								count++;
								break;
							default:
								// Got something we can't add up.
								// TODO: Add logging or an error
								break;
						}
					}

					yield return new PrtgResult
					{
						Channel = mapping.Name,
						Unit = mapping.Unit,
						CustomUnit = mapping.CustomUnit,
						Value = sum/count
					};

					w.WriteLine("Name: {0} Count: {1} Value: {2}",  property.Name, count, sum / count);
				}
			}
		}
	}

	public class RequestPrams
	{
		public string ServerAndPort { get; set; }
		public string User { get; set; }
		public string Password { get; set; }
		public string BucketName { get; set; }
	}
}
