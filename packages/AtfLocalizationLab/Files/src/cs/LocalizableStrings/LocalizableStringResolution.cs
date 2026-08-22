namespace AtfLocalizationLabApp.LocalizableStrings {

	/// <summary>
	/// Values returned by the supported Creatio localizable-string resolution paths.
	/// </summary>
	public sealed class LocalizableStringResolution {

		/// <summary>Gets or sets the value resolved using the active culture.</summary>
		public string CurrentCultureValue { get; set; }

		/// <summary>Gets or sets the value resolved strictly for the requested culture.</summary>
		public string StrictValue { get; set; }

		/// <summary>Gets or sets the value resolved with Creatio fallback enabled.</summary>
		public string FallbackValue { get; set; }
	}
}
