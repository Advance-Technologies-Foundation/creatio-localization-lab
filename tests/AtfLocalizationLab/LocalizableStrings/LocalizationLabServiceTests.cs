using System;
using System.Globalization;
using AtfLocalizationLabApp.LocalizableStrings;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace AtfLocalizationLab.Tests.LocalizableStrings {

	[TestFixture]
	[Category("Implementation")]
	public sealed class LocalizationLabServiceTests {
		[Test]
		[Description("The domain service requires the injectable localizable-string resolver.")]
		public void Constructor_WithoutResolver_Throws() {
			// Arrange
			Action construct = () => new LocalizationLabService(null);

			// Act
			Action act = construct;

			// Assert
			act.Should().Throw<ArgumentNullException>(
				because: "the domain service cannot resolve localizable values without its abstraction");
		}

		[Test]
		[Description("The lab domain service composes current, strict, and fallback operations through the abstraction.")]
		public void Resolve_UsesLocalizableStringAbstraction() {
			// Arrange
			ILocalizableStringResolver resolver = Substitute.For<ILocalizableStringResolver>();
			CultureInfo culture = CultureInfo.GetCultureInfo("es-ES");
			CultureInfo originalCulture = CultureInfo.CurrentCulture;
			resolver.GetValue("Schema", "Item").Returns(_ => CultureInfo.CurrentCulture.Name);
			resolver.GetCultureValue("Schema", "Item", culture).Returns("strict");
			resolver.GetCultureValueWithFallback("Schema", "Item", culture).Returns("fallback");
			ILocalizationLabService service = new LocalizationLabService(resolver);

			// Act
			LocalizableStringResolution result = service.Resolve("Schema", "Item", culture);

			// Assert
			result.CurrentCultureValue.Should().Be("es-ES",
				because: "the Value path must run under the explicitly requested execution culture");
			result.StrictValue.Should().Be("strict",
				because: "the domain service must preserve strict lookup semantics");
			result.FallbackValue.Should().Be("fallback",
				because: "the domain service must preserve fallback lookup semantics");
			resolver.Received(1).GetValue("Schema", "Item");
			resolver.Received(1).GetCultureValue("Schema", "Item", culture);
			resolver.Received(1).GetCultureValueWithFallback("Schema", "Item", culture);
			CultureInfo.CurrentCulture.Should().Be(originalCulture,
				because: "the domain service must restore ambient culture after current-culture resolution");
		}
	}
}
