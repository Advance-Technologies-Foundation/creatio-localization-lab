using System.Globalization;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Web.SessionState;
using AtfLocalizationLabApp.LocalizableStrings;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Terrasoft.Web.Common;

namespace AtfLocalizationLabApp.EntryPoints.WebServices {

	/// <summary>
	/// Validates localization-lab HTTP requests and delegates resolution to the application service.
	/// </summary>
	[ServiceContract]
	[AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Required)]
	public sealed class AtfLocalizationLabService : BaseService, IReadOnlySessionState {

		/// <summary>Resolves one schema-owned localizable value.</summary>
		/// <param name="request">Localization lookup request.</param>
		/// <returns>A concrete transport response.</returns>
		[OperationContract]
		[WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.Bare,
			RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
		public LocalizationResolutionResponse Resolve(LocalizationResolutionRequest request) {
			ErrorOr<CultureInfo> cultureOrError = ValidateRequiredFields(request);
			if (cultureOrError.IsError) {
				SetStatusCode(400);
				LocalizationResolutionResponse validationError = ValidationError(
					cultureOrError.FirstError.Description);
				return validationError;
			}
			CultureInfo culture = cultureOrError.Value;

			using (IServiceScope scope = AtfLocalizationLabApp.Instance.CreateScope()) {
				ILocalizationLabService service = scope.ServiceProvider
					.GetRequiredService<ILocalizationLabService>();
				LocalizableStringResolution result = service.Resolve(request.ResourceSchemaName,
					request.ResourceItemName, culture);
				LocalizationResolutionResponse response = LocalizationResolutionResponse.From(request,
					culture, result);
				return response;
			}
		}

		private static ErrorOr<CultureInfo> ValidateRequiredFields(
			LocalizationResolutionRequest request) {
			if (request == null) {
				Error error = Error.Validation(description: "Request is required.");
				return error;
			}
			if (string.IsNullOrWhiteSpace(request.ResourceSchemaName)) {
				Error error = Error.Validation(description: "Resource schema name is required.");
				return error;
			}
			if (string.IsNullOrWhiteSpace(request.ResourceItemName)) {
				Error error = Error.Validation(description: "Resource item name is required.");
				return error;
			}
			if (string.IsNullOrWhiteSpace(request.CultureName)) {
				Error error = Error.Validation(description: "Culture name is required.");
				return error;
			}
			try {
				CultureInfo culture = CultureInfo.GetCultureInfo(request.CultureName);
				return culture;
			}
			catch (CultureNotFoundException) {
				Error error = Error.Validation(description: "Culture name is invalid.");
				return error;
			}
		}

		private static LocalizationResolutionResponse ValidationError(string message) {
			LocalizationResolutionResponse response = new LocalizationResolutionResponse {
				Success = false,
				ErrorMessage = message
			};
			return response;
		}

		private void SetStatusCode(int statusCode) {
#if NETSTANDARD2_0
			HttpContextAccessor.GetInstance().Response.StatusCode = statusCode;
#else
			WebOperationContext context = WebOperationContext.Current;
			if (context != null) {
				context.OutgoingResponse.StatusCode = (HttpStatusCode)statusCode;
				return;
			}
			var httpContext = HttpContextAccessor?.GetInstance();
			if (httpContext != null) {
				httpContext.Response.StatusCode = statusCode;
			}
#endif
		}
	}

	/// <summary>Transport request for a schema-owned localizable value.</summary>
	[DataContract(Name = "localization-resolution-request")]
	public sealed class LocalizationResolutionRequest {

		/// <summary>Gets or sets the resource owner schema name.</summary>
		[DataMember(Name = "resourceSchemaName")]
		public string ResourceSchemaName { get; set; }

		/// <summary>Gets or sets the exact persisted resource item name.</summary>
		[DataMember(Name = "resourceItemName")]
		public string ResourceItemName { get; set; }

		/// <summary>Gets or sets the requested culture name.</summary>
		[DataMember(Name = "cultureName")]
		public string CultureName { get; set; }
	}

	/// <summary>Concrete transport response for a localization lookup.</summary>
	[DataContract(Name = "localization-resolution-response")]
	public sealed class LocalizationResolutionResponse {

		/// <summary>Gets or sets whether resolution succeeded.</summary>
		[DataMember(Name = "success")]
		public bool Success { get; set; }

		/// <summary>Gets or sets a validation error message.</summary>
		[DataMember(Name = "errorMessage")]
		public string ErrorMessage { get; set; }

		/// <summary>Gets or sets the resource owner schema name.</summary>
		[DataMember(Name = "resourceSchemaName")]
		public string ResourceSchemaName { get; set; }

		/// <summary>Gets or sets the exact persisted resource item name.</summary>
		[DataMember(Name = "resourceItemName")]
		public string ResourceItemName { get; set; }

		/// <summary>Gets or sets the requested culture name.</summary>
		[DataMember(Name = "cultureName")]
		public string CultureName { get; set; }

		/// <summary>Gets or sets the current-culture result.</summary>
		[DataMember(Name = "currentCultureValue")]
		public string CurrentCultureValue { get; set; }

		/// <summary>Gets or sets the strict-culture result.</summary>
		[DataMember(Name = "strictValue")]
		public string StrictValue { get; set; }

		/// <summary>Gets or sets the fallback result.</summary>
		[DataMember(Name = "fallbackValue")]
		public string FallbackValue { get; set; }

		internal static LocalizationResolutionResponse From(LocalizationResolutionRequest request,
			CultureInfo culture, LocalizableStringResolution result) {
			LocalizationResolutionResponse response = new LocalizationResolutionResponse {
				Success = true,
				ResourceSchemaName = request.ResourceSchemaName,
				ResourceItemName = request.ResourceItemName,
				CultureName = culture.Name,
				CurrentCultureValue = result.CurrentCultureValue,
				StrictValue = result.StrictValue,
				FallbackValue = result.FallbackValue
			};
			return response;
		}
	}
}
