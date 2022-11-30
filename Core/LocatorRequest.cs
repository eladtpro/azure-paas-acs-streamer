namespace RadioArchive
{
	public class LocatorContext
	{
		public const string ContainerName = "data";
        public string OriginalName { get; private set; }
        public string Name { get; set; }
		public IDictionary<string, StreamingPath> urls { get; set; }
		public BlobProperties Properties { get; set; }
		public Uri SasUri { get; set; }

		public LocatorContext(string name, BlobProperties props)
		{
			OriginalName = name;
            Name = name.Sanitize();
			Properties = props;
        }

		override public string ToString()
		{
			return $"LocatorContext: {Name}, {Properties}";
		}
	}
}

