namespace RadioArchive
{
	public class LocatorRequest
	{
        public string Name { get; set; }
		public Stream Blob { get; set; }
		public TimeSpan? start = null;
		public TimeSpan? end { get; set; } = null;

		public LocatorRequest(string name, Stream blob)
		{
			Name = name;
			Blob = blob;
        }

		override public string ToString()
		{
			return $"LocatorRequest: {Name}, {Blob.Length}, {start}, {end}";
		}
	}
}

