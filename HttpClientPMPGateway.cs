﻿using EastFive.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace EastFive.Appriss.PMPGateway
{
    /// <summary>
    /// This class is used to communicate with the Appriss PMP Gateway in order to pull 
    /// PMP records for patients.  The primary routine is PostPatientReportAsync, which is a
    /// convenience function that combines the patient and report POSTs into a single call.
    /// The class allows for the patient and report POSTs to be called individually, as well.
    /// </summary>
    public class HttpClientPMPGateway : IDisposable
    {
        private readonly Uri baseUri;
        private readonly string apiVersion;
        private readonly AuthenticationHeaderValue authenticationHeaderValue;

        private Lazy<HttpMessageHandler> handler;

        /// <summary>
        /// Class to hold patient info to send to request
        /// </summary>
        public class Patient
        {
            public string firstname;
            public string lastname;
            public string dob;
            public string gender;
            public string street1;
            public string street2;
            public string city;
            public string state;
            public string zip;

            private string internalPhone;
            public string phone
            {
                get
                {
                    return internalPhone;
                }
                set
                {
                    internalPhone = value.HasBlackSpace() ? value.Replace("-", "") : value;
                }
            }
        }

        /// <summary>
        /// Class to hold Provider info to send to request
        /// </summary>
        public class Provider
        {
            public string firstname;
            public string lastname;
            public string dea;
            public string npi;
            public string professionalLicense;
            public string professionalLicenseType;
            public string role;
            public string locationName;
            public string stateCode;
        }

        /// <summary>
        /// Constructor used to set up parameters common to every request
        /// </summary>
        /// <param name="baseUri">Set this to the sandbox or production Uri</param>
        /// <param name="apiVersion">Set this to v5 or another API version number</param>
        /// <param name="certificate">Certificate as a Base 64 encoded string</param>
        /// <param name="certificatePassword">The password for the pfx certificate</param>
        public HttpClientPMPGateway(string username, string password, Uri baseUri, string apiVersion, string certificate, string certificatePassword)
        {
            this.baseUri = baseUri;
            this.apiVersion = apiVersion;

            this.handler = new Lazy<HttpMessageHandler>(
                () =>
                {
                    byte[] certificateBytes = Convert.FromBase64String(certificate);
                    X509Certificate2 cer = new X509Certificate2(certificateBytes, certificatePassword, X509KeyStorageFlags.MachineKeySet);
                    var handler = new HttpClientHandler();
                    handler.ClientCertificates.Add(cer);
                    return handler;
                });
            var credentials = Encoding.ASCII.GetBytes($"{username}:{password}");
            this.authenticationHeaderValue = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));
        }

        private HttpClient GetClient()
        {
            var client = new HttpClient(this.handler.Value, false)
            {
                Timeout = new TimeSpan(0, 5, 0),
            };
            client.DefaultRequestHeaders.Authorization = authenticationHeaderValue;
            client.DefaultRequestHeaders.Add("Accept", "application/xml");
            return client;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (handler != null)
                {
                    if (handler.IsValueCreated)
                    {
                        handler.Value.Dispose();
                    }
                    handler = null;
                }
            }
        }

        ~HttpClientPMPGateway()
        {
            Dispose(false);
        }

        /// <summary>
        /// Used to POST patient information.  Must call PostReportAsync with the return from this call with the report Url.
        /// Alternatively, you may call PostPatientReportAsync, which wraps both of these calls in a single call for convenience.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="provider"></param>
        /// <param name="patient"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onBadRequest"></param>
        /// <param name="onUnauthorized"></param>
        /// <param name="onNotFound"></param>
        /// <param name="onInternalServerError"></param>
        /// <param name="onCouldNotIdentifyUniquePatient"></param>
        /// <param name="onPMPError"></param>
        /// <param name="onFailure"></param>
        /// <returns></returns>
        public async Task<TResult> PostPatientAsync<TResult>(Provider provider, Patient patient,
            Func<XDocument, TResult> onSuccess,
            Func<string, TResult> onBadRequest,
            Func<string, TResult> onUnauthorized,
            Func<string, TResult> onNotFound,
            Func<string, TResult> onInternalServerError,
            Func<string, TResult> onCouldNotIdentifyUniquePatient,
            Func<string, TResult> onPMPError,
            Func<string, TResult> onFailure)
        {
            string getError(XElement message, XElement details)
            {
                var messageStr = message != null ? message.Value : string.Empty;
                var detailStr = details != null ? details.Value : string.Empty;

                var error = messageStr;
                if (detailStr.HasBlackSpace() && !detailStr.StartsWith("Details of error can be"))
                    error += $" - {detailStr}";
                if (messageStr.Contains("not allowed to make requests"))
                    error += " If this is unexpected, you can go to Settings and change your Role.  We'll retry the request.";

                return error;
            }

            return await PostAsync(new Uri(baseUri, $"/{apiVersion}/patient"), CreatePatientRequestXML(provider, patient),
                (content) =>
                {
                    try
                    {
                        var settings = new XmlReaderSettings
                        {
                            DtdProcessing = DtdProcessing.Ignore, // prevents XXE attacks, such as Billion Laughs
                            MaxCharactersFromEntities = 1024,
                            XmlResolver = null,                   // prevents external entity DoS attacks, such as slow loading links or large file requests
                        };
                        using (var strReader = new StringReader(content))
                        using (var xmlReader = XmlReader.Create(strReader, settings))
                        {
                            var xml = XDocument.Load(xmlReader);
                            var disallowedNode = xml.Descendants().Where(x => x.Name.LocalName == "Disallowed").FirstOrDefault();
                            if (null != disallowedNode)
                            {
                                var message = disallowedNode.Descendants().Where(x => x.Name.LocalName == "Message").FirstOrDefault();
                                var details = disallowedNode.Descendants().Where(x => x.Name.LocalName == "Details").FirstOrDefault();
                                return onCouldNotIdentifyUniquePatient(getError(message, details));
                            }

                            //03/09/2019, KDH.  Defer error handling to caller as the response might include errors as well as reports from other states
                            //in this case, let the caller decide if they want to go with the reports they have or bail.
                            //var errorNode = xml.Descendants().Where(x => x.Name.LocalName == "Error").FirstOrDefault();
                            //if (null != errorNode)
                            //{
                            //    var message = errorNode.Descendants().Where(x => x.Name.LocalName == "Message").FirstOrDefault();
                            //    var details = errorNode.Descendants().Where(x => x.Name.LocalName == "Details").FirstOrDefault();
                            //    return onPMPError($"{message.Value} - {details.Value}");
                            //}
                            return onSuccess(xml);
                        }
                    }
                    catch (Exception ex)
                    {
                        return onFailure($"Could not parse XML content from PMP Gateway - {ex.Message}");
                    }
                },
                // onBadRequest
                (content) =>
                {
                    try
                    {
                        var settings = new XmlReaderSettings
                        {
                            DtdProcessing = DtdProcessing.Ignore, // prevents XXE attacks, such as Billion Laughs
                            MaxCharactersFromEntities = 1024,
                            XmlResolver = null,                   // prevents external entity DoS attacks, such as slow loading links or large file requests
                        };
                        using (var strReader = new StringReader(content))
                        using (var xmlReader = XmlReader.Create(strReader, settings))
                        {
                            var xml = XDocument.Load(xmlReader);
                            var errorNode = xml.Descendants().Where(x => x.Name.LocalName == "Error").FirstOrDefault();
                            if (null != errorNode)
                            {
                                var message = errorNode.Descendants().Where(x => x.Name.LocalName == "Message").FirstOrDefault();
                                var details = errorNode.Descendants().Where(x => x.Name.LocalName == "Details").FirstOrDefault();
                                return onBadRequest(getError(message, details));
                            }
                            return onBadRequest(content);
                        }
                    }
                    catch (Exception ex)
                    {
                        return onFailure($"Could not parse XML content from PMP Gateway - {ex.Message}");
                    }
                },
                onUnauthorized,
                onNotFound,
                onInternalServerError,
                onFailure);
        }

        /// <summary>
        /// Called after calling PostPatientAsync and getting the report link to get the report HTML
        /// Alternatively, you may call PostPatientReportAsync, which wraps both of these calls in a single call for convenience.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="provider"></param>
        /// <param name="reportLink"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onBadRequest"></param>
        /// <param name="onUnauthorized"></param>
        /// <param name="onNotFound"></param>
        /// <param name="onInternalServerError"></param>
        /// <param name="onFailure"></param>
        /// <returns></returns>
        public async Task<TResult> PostReportAsync<TResult>(Provider provider, string reportLink,
            Func<HtmlAgilityPack.HtmlDocument, TResult> onSuccess,
            Func<string, TResult> onBadRequest,
            Func<string, TResult> onUnauthorized,
            Func<string, TResult> onNotFound,
            Func<string, TResult> onInternalServerError,
            Func<string, TResult> onFailure)
        {
            return await PostAsync(new Uri(reportLink), CreateReportRequestXML(provider),
                (content) =>
                {
                    var htmlDocument = new HtmlAgilityPack.HtmlDocument();
                    try
                    {
                        htmlDocument.LoadHtml(content);
                    }
                    catch(Exception ex)
                    {
                        return onFailure($"Could not parse HTML content from PMP Gateway - {ex.Message}");
                    }
                    return onSuccess(htmlDocument);
                },
                onBadRequest,
                onUnauthorized,
                onNotFound,
                onInternalServerError,
                onFailure);
        }

        /// <summary>
        /// Convenience call that wraps PostPatientAsync and PostReportAsync in a single call for a single patient.  Returns
        /// report in an HtmlAgilityPack.HtmlDocument object on success. 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="provider"></param>
        /// <param name="patient"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onBadRequest"></param>
        /// <param name="onUnauthorized"></param>
        /// <param name="onNotFound"></param>
        /// <param name="onInternalServerError"></param>
        /// <param name="onCouldNotIdentifyUniquePatient"></param>
        /// <param name="onPMPError"></param>
        /// <param name="onFailure"></param>
        /// <returns></returns>
        public async Task<TResult> PostPatientReportAsync<TResult>(Provider provider, Patient patient,
            Func<HtmlAgilityPack.HtmlDocument, TResult> onSuccess,
            Func<string, TResult> onBadRequest,
            Func<string, TResult> onUnauthorized,
            Func<string, TResult> onNotFound,
            Func<string, TResult> onInternalServerError,
            Func<string, TResult> onCouldNotIdentifyUniquePatient,
            Func<string, TResult> onPMPError,
            Func<string, TResult> onFailure)
        {
            return await await PostPatientAsync(provider, patient,
                async (xmlDocument) =>
                {
                    var reportLinkNode = xmlDocument.Descendants().Where(x => x.Name.LocalName == "ViewableReport").FirstOrDefault();

                    //03/09/2019, KDH.  If we don't get a report node, check for an error node from the report.  Otherwise, general failure.
                    if (null == reportLinkNode)
                    {
                        var errorNode = xmlDocument.Descendants().Where(x => x.Name.LocalName == "Error").FirstOrDefault();
                        if (null != errorNode)
                        {
                            var message = errorNode.Descendants().Where(x => x.Name.LocalName == "Message").FirstOrDefault();
                            var details = errorNode.Descendants().Where(x => x.Name.LocalName == "Details").FirstOrDefault();
                            return onPMPError($"{message.Value} - {details.Value}");
                        }

                        return onFailure("Could not find ViewableReport node in patient XML");
                    }

                    return await PostReportAsync(provider, reportLinkNode.Value,
                        (htmlDocument) =>
                        {
                            return onSuccess(htmlDocument);
                        },
                        onBadRequest,
                        onUnauthorized,
                        onNotFound,
                        onInternalServerError,
                        onFailure);
                },
                (why)=> onBadRequest(why).AsTask(),
                (why) => onUnauthorized(why).AsTask(),
                (why) => onNotFound(why).AsTask(),
                (why) => onInternalServerError(why).AsTask(),
                (why) => onCouldNotIdentifyUniquePatient(why).AsTask(),
                (why) => onPMPError(why).AsTask(),
                (why) => onFailure(why).AsTask());
        }

        #region Supporting Routines

        private async Task<TResult> PostAsync<TResult>(Uri uri, string xmlContent,
            Func<string, TResult> onSuccess,
            Func<string, TResult> onBadRequest,
            Func<string, TResult> onUnauthorized,
            Func<string, TResult> onNotFound,
            Func<string, TResult> onInternalServerError,
            Func<string, TResult> onFailure)
        {
            var request = new HttpRequestMessage(
                new HttpMethod("POST"),
                uri);

            var finalContentString = new StringContent(xmlContent, Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Content = finalContentString;

            string content;
            HttpStatusCode statusCode;
            string reasonPhrase;

            using (var client = GetClient())
            using (var response = await client.SendAsync(request))
            {
                content = await response.Content.ReadAsStringAsync();
                statusCode = response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
            }

            switch (statusCode)
            {
                case HttpStatusCode.OK:
                    return onSuccess(content);

                case HttpStatusCode.BadRequest:
                    return onBadRequest(content);

                case HttpStatusCode.Unauthorized:
                    return onUnauthorized(content);

                case HttpStatusCode.NotFound:
                    return onNotFound(content);

                case HttpStatusCode.InternalServerError:
                    return onInternalServerError(content);

                default:
                    return onFailure($"{reasonPhrase} - {content}");
            }
        }

        private static string CreatePatientRequestXML(Provider provider, Patient patient)
        {
            //<?xml version="1.0" encoding="UTF-8"?>
            //<PatientRequest xmlns="http://xml.appriss.com/gateway/v5">
            //  <Requester>
            //    <Provider>
            //      <Role>Physician</Role>
            //      <FirstName>Jon</FirstName>
            //      <LastName>Doe</LastName>
            //      <DEANumber>AB1234579</DEANumber>
            //    </Provider>
            //    <Location>
            //      <Name>Store #123</Name>
            //      <DEANumber>AB1234579</DEANumber>
            //      <Address>
            //        <StateCode>KS</StateCode>
            //      </Address>
            //    </Location>
            //  </Requester>
            //  <PrescriptionRequest>
            //    <Patient>
            //      <Name>
            //        <First>Bob</First>
            //        <Last>Testpatient</Last>
            //      </Name>
            //      <Birthdate>1900-01-01</Birthdate>
            //      <SexCode>M</SexCode>
            //      <!-- ZipCode or Phone is required. -->
            //      <Address>
            //        <Street></Street>
            //        <Street></Street>
            //        <City></City>
            //        <StateCode>IN</StateCode>
            //        <ZipCode>67203</ZipCode>
            //      </Address>
            //      <Phone>1234567890</Phone>
            //    </Patient>
            // </PrescriptionRequest>
            //</PatientRequest>

            XDocument doc = new XDocument(
                new XElement("PatientRequest",
                    new XElement("Requester",
                        new XElement("Provider",
                            new XElement("Role", provider.role),
                            new XElement("FirstName", provider.firstname),
                            new XElement("LastName", provider.lastname),
                            new XElement("DEANumber", provider.dea)),
                        new XElement("Location",
                            new XElement("Name", provider.locationName),
                            new XElement("DEANumber", provider.dea),
                            new XElement("Address",
                                new XElement("StateCode", provider.stateCode)))),
                    new XElement("PrescriptionRequest",
                        new XElement("Patient",
                            new XElement("Name",
                                new XElement("First", patient.firstname),
                                new XElement("Last", patient.lastname)),
                            new XElement("Birthdate", patient.dob),
                            new XElement("SexCode", patient.gender),
                            new XElement("Address",
                                new XElement("Street", patient.street1),
                                new XElement("Street", patient.street2),
                                new XElement("City", patient.city),
                                //,new XElement("StateCode", patient.state),
                                new XElement("ZipCode", patient.zip))
                            //,new XElement("Phone", patient.phone)
                                ))));

            // These enumeration elements cannot be empty so add them later if filled in
            if (patient.state.HasBlackSpace())
            {
                var cityNode = doc.Descendants().Where(x => x.Name.LocalName == "City").FirstOrDefault();
                if (null != cityNode)
                {
                    cityNode.AddAfterSelf(new XElement("StateCode", patient.state));
                }
            }
            if (patient.phone.HasBlackSpace())
            {
                var patientNode = doc.Descendants().Where(x => x.Name.LocalName == "Patient").FirstOrDefault();
                if (null != patientNode)
                {
                    patientNode.Add(new XElement("Phone", patient.phone));
                }
            }

            var xml = doc.ToString();
            var editedXML = xml.Replace("<PatientRequest>", "<PatientRequest xmlns=\"http://xml.appriss.com/gateway/v5\">");
            return editedXML;
        }

        private static string CreateReportRequestXML(Provider provider)
        {
            //<? xml version = "1.0" encoding = "UTF-8" ?>
            //< ReportRequest xmlns = "http://xml.appriss.com/gateway/v5" >
            //     < Requester >
            //       < ReportLink />< !--Include this element to get the report as a on - time - use URL. -- >
            //            < Provider >< !--Person viewing the report. -- >
            //                < Role > Physician </ Role >
            //                < FirstName > Jon </ FirstName >
            //                < LastName > Doe </ LastName >
            //                < !--At least one identifier is required: DEANumber, NPINumber, or ProfessionalLicenseNumber. -->
            //                < DEANumber > AB1234579 </ DEANumber >
            //              </ Provider >
            //              < Location >
            //                < Name > Store #123</Name>
            //      < !--At least one identifier is required: DEANumber, NPINumber, or NCPDPNumber. -->
            //      < DEANumber > AB1234579 </ DEANumber >
            //      < Address >
            //        < StateCode > OH </ StateCode >
            //      </ Address >
            //    </ Location >
            //  </ Requester >
            //</ ReportRequest >

            XDocument doc = new XDocument(
                new XElement("ReportRequest",
                    new XElement("Requester",
                        //new XElement("ReportLink", reportLink),
                        new XElement("Provider",
                            new XElement("Role", provider.role),
                            new XElement("FirstName", provider.firstname),
                            new XElement("LastName", provider.lastname),
                            new XElement("DEANumber", provider.dea)),
                        new XElement("Location",
                            new XElement("Name", provider.locationName),
                            new XElement("DEANumber", provider.dea),
                            new XElement("Address",
                                new XElement("StateCode", provider.stateCode))))));
            var xml = doc.ToString();
            var editedXML = xml.Replace("<ReportRequest>", "<ReportRequest xmlns=\"http://xml.appriss.com/gateway/v5\">");
            return editedXML;
        }
        #endregion
    }
}
