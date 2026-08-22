using System;
using System.Collections.Generic;
using System.Globalization;
using AtfLocalizationLabApp.EntryPoints.WebServices;
using AtfLocalizationLabApp.LocalizableStrings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Terrasoft.Core.ServiceModelContract;
using Terrasoft.Web.Http.Abstractions;
using App = AtfLocalizationLabApp.AtfLocalizationLabApp;

namespace AtfLocalizationLab.Tests.EntryPoints.WebServices {

	[TestFixture]
	[Category("Implementation")]
	public sealed class AtfLocalizationLabServiceTests {

		private ILocalizationLabService _domainService;
		private HttpResponse _httpResponse;
		private AtfLocalizationLabService _service;

		[SetUp]
		public void SetUp() {
			_domainService = Substitute.For<ILocalizationLabService>();
			App.InjectedServices = new List<Func<IServiceCollection, IServiceCollection>> {
				services => services.AddSingleton(_domainService)
			};
			App.Instance.Reset();

			HttpContext context = Substitute.For<HttpContext>();
			_httpResponse = Substitute.For<HttpResponse>();
			context.Response.Returns(_httpResponse);
			IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
			accessor.GetInstance().Returns(context);
			_service = new AtfLocalizationLabService { HttpContextAccessor = accessor };
		}

		[TearDown]
		public void TearDown() {
			App.InjectedServices = null;
			App.Instance.Reset();
		}

		[Test]
		[Description("A valid request is delegated to the injectable localizable-string resolver.")]
		public void Resolve_ValidRequest_DelegatesToResolver() {
			// Arrange
			LocalizationResolutionRequest request = new LocalizationResolutionRequest {
				ResourceSchemaName = "AtfLocalizationLabMessages",
				ResourceItemName = "LocalizableStrings.SharedGreeting.Value",
				CultureName = "es-ES"
			};
			_domainService.Resolve(request.ResourceSchemaName, request.ResourceItemName,
				Arg.Is<CultureInfo>(culture => culture.Name == request.CultureName))
				.Returns(new LocalizableStringResolution {
					CurrentCultureValue = "current",
					StrictValue = "strict",
					FallbackValue = "fallback"
				});

			// Act
			LocalizationResolutionResponse response = _service.Resolve(request);

			// Assert
			response.Success.Should().BeTrue(
				because: "the domain resolver returned a successful resolution");
			response.StrictValue.Should().Be("strict",
				because: "the endpoint must map the domain result without performing localization itself");
			_domainService.Received(1).Resolve(request.ResourceSchemaName, request.ResourceItemName,
				Arg.Is<CultureInfo>(culture => culture.Name == "es-ES"));
		}

		[TestCase(true, null, null, null, "Request is required.")]
		[TestCase(false, null, "LocalizableStrings.SharedGreeting.Value", "es-ES",
			"Resource schema name is required.")]
		[TestCase(false, "AtfLocalizationLabMessages", null, "es-ES",
			"Resource item name is required.")]
		[TestCase(false, "AtfLocalizationLabMessages", "LocalizableStrings.SharedGreeting.Value", null,
			"Culture name is required.")]
		[TestCase(false, "AtfLocalizationLabMessages", "LocalizableStrings.SharedGreeting.Value",
			"invalid-culture", "Culture name is invalid.")]
		[Description("Invalid transport input is rejected before the domain resolver is invoked.")]
		public void Resolve_InvalidRequest_ReturnsBadRequest(bool requestIsNull, string resourceSchemaName,
			string resourceItemName, string cultureName, string expectedMessage) {
			// Arrange
			LocalizationResolutionRequest request = null;
			if (!requestIsNull) {
				request = new LocalizationResolutionRequest {
					ResourceSchemaName = resourceSchemaName,
					ResourceItemName = resourceItemName,
					CultureName = cultureName
				};
			}

			// Act
			LocalizationResolutionResponse response = _service.Resolve(request);

			// Assert
			response.Success.Should().BeFalse(
				because: "invalid transport input must not be reported as a successful lookup");
			response.ErrorMessage.Should().Be(expectedMessage,
				because: "the caller needs an actionable validation failure");
			_httpResponse.StatusCode.Should().Be(400,
				because: "invalid request data is an HTTP client error");
			_domainService.DidNotReceiveWithAnyArgs().Resolve(default, default, default);
		}
	}
}
