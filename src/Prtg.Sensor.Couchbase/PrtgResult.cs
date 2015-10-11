using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Prtg.Sensor
{
	// ReSharper disable InconsistentNaming
	[XmlRoot("prtg")]
	public class Prtg
	{
		private List<PrtgResult> _results;

		[XmlElement("result")]
		public List<PrtgResult> Results
		{
			get { return _results ?? (_results = new List<PrtgResult>()); }
			set { _results = value; }
		}

		[XmlElement("Error")]
		public int Error { get; set; }

		public bool ShouldSerializeError()
		{
			return Error != 0;
		}

		public string Text { get; set; }

		public void Add(PrtgResult prtgResult)
		{
			Results.Add(prtgResult);
		}

		public override string ToString()
		{
			return string.Join("\r\n", Results);
		}

		public void WriteToConsole()
		{
			var s = new XmlSerializer(typeof(Prtg));
			s.Serialize(Console.Out, this);
		}
	}

	public enum PrtgExitCode
	{
		// Return Codes
		// 0	OK
		Ok = 0,
		// 1	WARNING
		Warning = 1,
		// 2	System Error (e.g. a network/socket error)
		SystemError = 2,
		// 3	Protocol Error (e.g. web server returns a 404)
		ProtocalError = 3,
		// 4	Content Error (e.g. a web page does not contain a required word
		ContentError = 4
	}

	/// <summary>
	/// The PRTG Result, representing the values in a given channel
	/// </summary>
	/// <remarks>
	/// Documentation is from PRTG API help page on a Version 15.3 server.
	/// 
	/// As of now, we have not included most of the configuration parameters that can be changed from the PRTG console.
	/// </remarks>
	public class PrtgResult
	{
		private string _customUnit;
		private int _float = 1;
		private int _showChart = 1;
		private int _showTable = 1;

		/// <summary>
		/// Name of the channel as displayed in user interfaces. This parameter is required and must be unique for the sensor.
		/// </summary>
		public string Channel { get; set; }

		/// <summary>
		/// The value as integer or float. Please make sure the Float setting matches the kind of value provided. Otherwise PRTG will show 0 values.
		/// </summary>
		public double Value { get; set; }

		/// <summary>
		/// The unit of the value. Default is Custom. Useful for PRTG to be able to convert volumes and times.
		/// </summary>
		public ChannelUnit Unit { get; set; }

		/// <summary>
		/// If Custom is used as unit this is the text displayed behind the value
		/// </summary>
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

		/// <summary>
		/// Define if the value is a float. Default is 0 (no). If set to 1 (yes), use a dot as decimal separator in values. Note: Define decimal places with the DecimalMode element.
		/// </summary>
		public int Float
		{
			get { return _float; }
			set { _float = value; }
		}

		/// <summary>
		/// Init value for the Show in Chart option. Default is 1 (yes). Note: The values defined with this element will be considered only on the first sensor scan, when the channel is newly created; they are ignored on all further sensor scans (and may be omitted). You can change this initial setting later in the Channel settings of the sensor.
		/// </summary>
		public int ShowChart
		{
			get { return _showChart; }
			set { _showChart = value; }
		}

		/// <summary>
		/// Init value for the Show in Table option. Default is 1 (yes). Note: The values defined with this element will be considered only on the first sensor scan, when the channel is newly created; they are ignored on all further sensor scans (and may be omitted). You can change this initial setting later in the Channel settings of the sensor.
		/// </summary>
		public int ShowTable
		{
			get { return _showTable; }
			set { _showTable = value; }
		}

		/// <summary>
		/// Size used for the display value. E.g. if you have a value of 50000 and use Kilo as size the display is 50 kilo #. Default is One (value used as returned). For the Bytes and Speed units this is overridden by the setting in the user interface.
		/// </summary>
		public PrtgSize? SpeedSize { get; set; }

		public bool SpeedSizeSpecified => SpeedSize.HasValue;

		/// <summary>
		/// Size used for the display value. E.g. if you have a value of 50000 and use Kilo as size the display is 50 kilo #. Default is One (value used as returned). For the Bytes and Speed units this is overridden by the setting in the user interface.
		/// </summary>
		public PrtgSize? VolumeSize { get; set; }

		public bool VolumeSizeSpecified => VolumeSize.HasValue;

		public override string ToString()
		{
			return string.Format("{0} : {1} {2}", Channel ?? "N/A", Value, CustomUnit ?? Unit.ToString());
		}
	}

	public enum PrtgSize
	{
		One,
		Kilo,
		Mega,
		Giga,
		Tera,
		Byte,
		KiloByte,
		MegaByte,
		GigaByte,
		TeraByte,
		Bit,
		KiloBit,
		MegaBit,
		GigaBit,
		TeraBit,
	}

	public enum ChannelUnit
	{
		Custom,
		BytesBandwidth,
		BytesMemory,
		BytesDisk,
		Temperature,
		Percent,
		TimeResponse,
		TimeSeconds,
		Count,
		[XmlEnum("CPU (*)")]
		Cpu,
		BytesFile,
		SpeedDisk,
		SpeedNet,
		TimeHours,
	}
}
