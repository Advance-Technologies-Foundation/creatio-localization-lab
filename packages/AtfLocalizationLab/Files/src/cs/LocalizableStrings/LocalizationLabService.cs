using System;
using System.Globalization;

namespace AtfLocalizationLabApp.LocalizableStrings {

	/// <summary>
	/// Defines the lab use case that compares Creatio localizable-string resolution paths.
	/// </summary>
	public interface ILocalizationLabService {

		/// <summary>Runs the lab comparison for one schema-owned resource.</summary>
		/// <param name="resourceSchemaName">Name of the schema that owns the resource.</param>
		/// <param name="resourceItemName">Exact persisted resource item name.</param>
		/// <param name="culture">Culture to resolve.</param>
		/// <returns>Values produced by the three resolution paths.</returns>
		LocalizableStringResolution Resolve(string resourceSchemaName, string resourceItemName,
			CultureInfo culture);
	}

	/// <summary>
	/// Runs the localization lab through the injectable localizable-string abstraction.
	/// </summary>
	public sealed class LocalizationLabService : ILocalizationLabService {

		private readonly ILocalizableStringResolver _resolver;

		/// <summary>Initializes the lab service.</summary>
		/// <param name="resolver">Creatio localizable-string abstraction.</param>
		public LocalizationLabService(ILocalizableStringResolver resolver) {
			_resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
		}

		/// <inheritdoc />
		public LocalizableStringResolution Resolve(string resourceSchemaName, string resourceItemName,
			CultureInfo culture) {
			CultureInfo previousCulture = CultureInfo.CurrentCulture;
			string currentCultureValue;
			try {
				CultureInfo.CurrentCulture = culture;
				currentCultureValue = _resolver.GetValue(resourceSchemaName, resourceItemName);
			}
			finally {
				CultureInfo.CurrentCulture = previousCulture;
			}

			LocalizableStringResolution resolution = new LocalizableStringResolution {
				CurrentCultureValue = currentCultureValue,
				StrictValue = _resolver.GetCultureValue(resourceSchemaName, resourceItemName, culture),
				FallbackValue = _resolver.GetCultureValueWithFallback(resourceSchemaName, resourceItemName, culture)
			};
			return resolution;
		}
	}
}
