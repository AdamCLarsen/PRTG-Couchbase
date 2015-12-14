using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
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
			if (args.Length != 4)
			{
				if (args.Length > 0 && args[0].Equals("protect"))
				{
					Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
					ConfigurationSection section = config.GetSection("connectionStrings");
					{
						if (!section.SectionInformation.IsProtected)
						{
							if (!section.ElementInformation.IsLocked)
							{
								section.SectionInformation.ProtectSection("DataProtectionConfigurationProvider");
								section.SectionInformation.ForceSave = true;
								config.Save(ConfigurationSaveMode.Full);

								Console.WriteLine();
								Console.WriteLine("Encrypted Configuration File");
							}
						}
					}
				}
				else
				{
					Console.WriteLine("Missing Required arguments");
					Console.WriteLine("<cluster> bucket <bucket name> <mapping File>");
					Console.WriteLine("Examples:");
					Console.WriteLine("localhost bucket beer-sample basic");
				}

				Environment.Exit(2);
			}

			if (!args[1].Equals("bucket", StringComparison.OrdinalIgnoreCase))
			{
				Console.Write("Only Bucket view is currently supported.");
				Environment.Exit(2);
			}

			try
			{
				var location = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(Program)).Location);
				// Remove anything that may have been put in the file name that shouldn't be there.
				// The configuration file must be in the same folder as the .exe, and be a json files.
				var configurationFile = Path.Combine(location, Path.GetFileNameWithoutExtension(args[3] + ".json"));

				_mappings = JsonConvert.DeserializeObject<StatsMappings>(File.ReadAllText(configurationFile));

				var connectionString = new DbConnectionStringBuilder { ConnectionString = ConfigurationManager.ConnectionStrings[args[0]].ConnectionString };
				var result = Read(new RequestPrams
				{
					ServerAndPort = connectionString["servers"].ToString().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
					BucketName = args[2],
                    User = connectionString["user"].ToString(),
					Password = connectionString["password"].ToString(),
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
		/// <exception cref="WebSocketException">Condition.</exception>
		/// <exception cref="WebException">Condition.</exception>
		public static async Task<Prtg> Read(RequestPrams request)
		{
			// Allow for multiple server.  If the first server returns any kind of error, we will try the next servers.
			using (var servers = request.ServerAndPort.GetEnumerator())
			{
				if (!servers.MoveNext())
					throw new ArgumentException("No Servers specified", nameof(request));

				while (true)
				{
					try
					{
						using (var client = new HttpClient
						{
							BaseAddress = new Uri(servers.Current + "/pools/default/buckets/")
						})
						{
							var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(request.User + ":" + request.Password));
							client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

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
					}
					// If it's an exception that we know is from connecting to the server
					// let's try the next one
					// TODO: Report back error status.
					catch (WebSocketException)
					{
						if (!servers.MoveNext())
							throw;
					}
					catch (WebException)
					{
						if (!servers.MoveNext())
							throw;
					}
					catch (HttpRequestException)
					{
						if (!servers.MoveNext())
							throw;
					}
				}
			}
		}

		private static IEnumerable<PrtgResult> ReadData(JToken token)
		{
			var sampleToken = token.SelectToken(@"op.samples", true);
			//using (var s = File.Create("output.txt")) // write the results to disk for debugging.
			//using (var w = new StreamWriter(s))
			//{
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
					//  and we will assume here that Zoom is at a minute
					//
					// For now we are going to only support averaging the values.
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

					var avgValue = sum / count;
					if (!mapping.Float)
						avgValue = Math.Round(avgValue);

					yield return new PrtgResult
					{
						Channel = mapping.Name,
						Unit = mapping.Unit,
						CustomUnit = mapping.CustomUnit,
						Float = mapping.Float ? 1 : 0,
						Value = avgValue,
						SpeedSize = mapping.SpeedSize,
						VolumeSize = mapping.VolumeSize
					};

					//w.WriteLine("Name: {0} Count: {1} Value: {2}", property.Name, count, avgValue);
				//}
			}
		}
	}

	public class RequestPrams
	{
		public List<string> ServerAndPort { get; set; }
		public string User { get; set; }
		public string Password { get; set; }
		public string BucketName { get; set; }
	}
}
