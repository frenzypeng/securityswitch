// =================================================================================
// Copyright � 2004-2011 Matt Sollars
// All rights reserved.
// 
// This code and information is provided "as is" without warranty of any kind,
// either expressed or implied, including, but not limited to, the implied 
// warranties of merchantability and/or fitness for a particular purpose.
// =================================================================================
using System;
using System.Web;
using System.Web.Configuration;

using SecuritySwitch.Abstractions;
using SecuritySwitch.Configuration;
using SecuritySwitch.Evaluation;
using SecuritySwitch.Redirection;


namespace SecuritySwitch {
	/// <summary>
	/// Evaluates each request for the need to switch to HTTP/HTTPS.
	/// </summary>
	public class SecuritySwitchModule : IHttpModule {
		// Cached copy of the module's settings for reuse during this request.
		private Settings _settings;


		/// <summary>
		/// Initializes a module and prepares it to handle requests.
		/// </summary>
		/// <param name="context">
		/// An <see cref="T:System.Web.HttpApplication"/> that provides access to the methods, properties, and events common to 
		/// all application objects within an ASP.NET application.
		/// </param>
		public void Init(HttpApplication context) {
			if (context == null) {
				return;
			}

			// Get the settings for the securitySwitch section.
			_settings = WebConfigurationManager.GetSection("securitySwitch") as Settings;
			if (_settings == null || _settings.Mode == Mode.Off) {
				return;
			}

			// Hook the application's AcquireRequestState event.
			// * This ensures that the session ID is available for cookie-less session processing.
			// * I would rather hook sooner into the pipeline, but...
			// * It just is not possible (that I know of) to get the original URL requested when cookie-less sessions are used.
			//   The Framework uses RewritePath when the HttpContext is created to strip the Session ID from the request's 
			//   Path/Url. The rewritten URL is actually stored in an internal field of HttpRequest; short of reflection, 
			//   it's not obtainable.
			// WARNING: Do not access the Form collection of the HttpRequest object to avoid weird issues with post-backs from the application root.
			context.AcquireRequestState += ProcessRequest;
		}

		/// <summary>
		/// Disposes of the resources (other than memory) used by the module that implements <see cref="T:System.Web.IHttpModule"/>.
		/// </summary>
		public void Dispose() {}


		/// <summary>
		/// Processes the request, evaluating it for the need to redirect based on configured settings.
		/// </summary>
		/// <param name="sender">The sender.</param>
		/// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
		private void ProcessRequest(object sender, EventArgs e) {
			// Cast the source as an HttpApplication instance.
			var application = sender as HttpApplication;
			if (application == null) {
				return;
			}

			// Wrap the application's context (for testability) and process it.
			var context = new HttpContextWrapper(application.Context);
			ProcessRequest(context);
		}

		/// <summary>
		/// Processes the request.
		/// </summary>
		/// <param name="context">The context in which the request to process is running.</param>
		protected void ProcessRequest(HttpContextBase context) {
			HttpRequestBase request = context.Request;
			HttpResponseBase response = context.Response;

			// Raise the EvaluateRequest event and check if a subscriber indicated the security for the current request.
			var eventArgs = new EvaluateRequestEventArgs(context, _settings);
			InvokeEvaluateRequest(eventArgs);

			RequestSecurity expectedSecurity;
			if (eventArgs.ExpectedSecurity.HasValue) {
				// Use the vlaue returned by the EvaluateRequest event.
				expectedSecurity = eventArgs.ExpectedSecurity.Value;
			} else {
				// Evaluate this request with the configured settings, if necessary.
				IRequestEvaluator requestEvaluator = RequestEvaluatorFactory.Create();
				expectedSecurity = requestEvaluator.Evaluate(request, _settings);
			}

			if (expectedSecurity == RequestSecurity.Ignore) {
				// No action is needed for a result of Ignore.
				return;
			}

			// Ensure the request matches the expected security.
			ISecurityEvaluator securityEvaluator = SecurityEvaluatorFactory.Create(request, _settings);
			ISecurityEnforcer securityEnforcer = SecurityEnforcerFactory.Create(securityEvaluator);
			string targetUrl = securityEnforcer.GetUriForMatchedSecurityRequest(request, response, expectedSecurity, _settings);
			if (string.IsNullOrEmpty(targetUrl)) {
				// No action is needed if the security enforcer did not return a target URL.
				return;
			}

			// Redirect.
			ILocationRedirector redirector = LocationRedirectorFactory.Create();
			redirector.Redirect(response, targetUrl, _settings.BypassSecurityWarning);
		}


		/// <summary>
		/// Raised before the SecureSwitchModule evaluates the current request to allow subscribers a chance to evaluate the request.
		/// </summary>
		public event EvaluateRequestEventHandler EvaluateRequest;

		/// <summary>
		/// Raises the EvaluateRequest event.
		/// </summary>
		/// <param name="args">The EvaluateRequestEventArgs used by any event handler(s).</param>
		protected void InvokeEvaluateRequest(EvaluateRequestEventArgs args) {
			var handler = EvaluateRequest;
			if (handler != null) {
				handler(this, args);
			}
		}
	}


	/// <summary>
	/// The delegate for handlers of the EvaluateRequest event.
	/// </summary>
	public delegate void EvaluateRequestEventHandler(object sender, EvaluateRequestEventArgs args);
}