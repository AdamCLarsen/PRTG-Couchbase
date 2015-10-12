using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Prtg.Sensor.Couchbase.Config
{
	public class StatsMappings
	{
		private Dictionary<string, Channel> _values;

		public Dictionary<string, Channel> Channels
		{
			get { return _values ?? (_values = new Dictionary<string, Channel>()); }
			set { _values = value; }
		}

		public class Channel
		{
			private string _customUnit;
			public string Name { get; set; }

			public ChannelUnit Unit { get; set; }

			[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
			public string CustomUnit
			{
				get { return _customUnit; }
				set
				{
					if (value != null)
					{
						Unit = ChannelUnit.Custom;
					}

					_customUnit = value;
				}
			}

			[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
			public bool Float { get; set; }

			[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
			public PrtgSize? SpeedSize { get; set; }

			[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
			public PrtgSize? VolumeSize { get; set; }
		}
	}
}
