using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EastFive.Appriss.PMPGateway;
using System.Threading.Tasks;
using BlackBarLabs.Extensions;
using System.Linq;
using System.Xml.Linq;
using System.Configuration;

namespace EastFive.Appriss.PMPGateway.Test
{
    [TestClass]
    public class PMPGatewayTest
    {
        private string username = "";
        private string password = "";
        private Uri baseUri = null;
        private string apiVersion = "";
        private string pmpCertificate = "";
        private string certificatePassword = "";
        private readonly HttpClientPMPGateway.Provider provider;

        public PMPGatewayTest()
        {
            username = ConfigurationManager.AppSettings.Get("PMPUsername");
            password = ConfigurationManager.AppSettings.Get("PMPPassword");
            baseUri = new Uri(ConfigurationManager.AppSettings.Get("PMPBaseUri"));
            apiVersion = ConfigurationManager.AppSettings.Get("PMPAPIVersion");
            pmpCertificate = ConfigurationManager.AppSettings.Get("PMPCertificate");
            certificatePassword = ConfigurationManager.AppSettings.Get("PMPCertificatePassword");

            provider = new HttpClientPMPGateway.Provider
            {
                firstname = "Paul",  //REQUIRED
                lastname = "Doctor", //REQUIRED 
                dea = "AD1111119", //REQUIRED
                //role = "Prescriber Delegate - Licensed", //REQUIRED - {'Physician', 'Pharmacist', 'Pharmacist with prescriptive authority', 'Nurse Practitioner', 'Psychologist with prescriptive authority', 'Optometrist with prescriptive authority', 'Naturopathic Physician with prescriptive authority', 'Physician Assistant with prescriptive authority', 'Medical Resident with prescriptive authority', 'Medical Intern with prescriptive authority', 'Dentist', 'Medical Resident with no independent prescriptive authority', 'Medical Intern with no independent prescriptive authority', 'Prescriber Delegate - Licensed', 'Prescriber Delegate - Unlicensed', 'Pharmacist's Delegate - Licensed', 'Pharmacist's Delegate - Unlicensed', 'Other - Non Prescriber', 'Other Prescriber'}
                role = "Physician",
                stateCode = "KS",  //REQUIRED

                //npi = "1023011178",
                //professionalLicense = "F458548",
                //professionalLicenseType = "ADM",
                //locationName = ""
            };
        }


        [TestMethod]
        public async Task PMPGatewayReportUsingPostPatientAndPostReportSeparately()
        {
            //Multiple prescriptions
            //Additional details: demonstrates various scenarios; moved at some point(address changes: 789 Niagara
            //Road, Columbus, OH, 43232), typical variation in first name(Abigail), typo in first name(Abigale), moved
            //and also a variation in first name(Abbi; 99 Scoop St, Wooster, OH, 44100). 
            var patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Abby",
                lastname = "Testpatient",
                dob = "1970-07-01",
                gender = "Female",
                street = "165 Parkview Rd",
                city = "Columbus",
                state = "OH",
                zip = "43209",
                phone = "614-555-1994"
            };
            Assert.IsTrue(
                await GetReportAsync(patient)
                );
        }

        [TestMethod]
        public async Task PMPGatewayPatientReport_Single()
        {
            var patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Kyle",
                lastname = "Duncan",
                dob = "1986-09-24",
                //gender = "Male",
                //street = "165 Parkview Rd",
                //city = "Columbus",
                //state = "OH",
                //zip = "43209",
                //phone = "614-555-1994"
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );
        }

        [TestMethod]
        public async Task PMPGatewayPatientReport_MultipleScenarios()
        {
            //Multiple prescriptions
            //Additional details: demonstrates various scenarios; moved at some point(address changes: 789 Niagara
            //Road, Columbus, OH, 43232), typical variation in first name(Abigail), typo in first name(Abigale), moved
            //and also a variation in first name(Abbi; 99 Scoop St, Wooster, OH, 44100). 
            //var patient = new HttpClientPMPGateway.Patient
            //{
            //    firstname = "Abby",
            //    lastname = "Testpatient",
            //    dob = "1970-07-01",
            //    gender = "Female",
            //    street = "165 Parkview Rd",
            //    city = "Columbus",
            //    state = "OH",
            //    zip = "43209",
            //    phone = "614-555-1994"
            //};
            //Assert.IsTrue(
            //    await GetPatientReportAsync(patient,
            //        (report) => true,
            //        (why) => false,
            //        (why) => false,
            //        (why) => false,
            //        (why) => false,
            //        (why) => false,
            //        (why) => false,
            //        (why) => false)
            //    );


            var patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Alice",
                lastname = "Testpatient",
                dob = "1900-01-01",
                gender = "Female",
                street = "",
                city = "",
                state = "KS",
                zip = "67203",
                phone = ""
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );

            //Additional details: Person commonly used by OARRS to demonstrate a good selection of prescription results
            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Betty",
                lastname = "Testpatient",
                dob = "1970-01-01",
                gender = "Female",
                street = "123 Broadway",
                city = "Columbus",
                state = "OH",
                zip = "43215",
                phone = "614-547-8798"
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );

            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Bob",
                lastname = "Testpatient",
                dob = "1900-01-01",
                gender = "Male",
                street = "1023 Not Real St",
                city = "Witchita",
                state = "KS",
                zip = "67203",
                phone = ""
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => true,
                    (why) => false,
                    (why) => false)
                );


            //Additional details: person who may be abusing prescription medication with a very high number of prescriptions (overlapping from different doctors, dispensers, etc.)
            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Cameron",
                lastname = "Testpatient",
                dob = "1980-08-08",
                gender = "Male",
                street = "123 Rhodes Way",
                city = "Columbus",
                state = "OH",
                zip = "43215",
                phone = "614-555-1111"
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );


            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Carol",
                lastname = "Testpatient",
                dob = "1900-01-01",
                gender = "Female",
                street = "123 Fake Patient St",
                city = "Witchita",
                state = "KS",
                zip = "67203",
                phone = ""
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );


            //Additional details: person with many prescriptions, providing a good example of a real person with one of the highest number of prescriptions in the system (no indication of medication abuse)
            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Chad",
                lastname = "Testpatient",
                dob = "1970-02-01",
                gender = "Male",
                street = "555 N. Dale Drive",
                city = "Disney",
                state = "OH",
                zip = "43215",
                phone = "614-555-1956"
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );


            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Dave",
                lastname = "Testpatient",
                dob = "1900-01-01",
                gender = "Male",
                street = "832 Not Real Patient Dr",
                city = "Witchita",
                state = "KS",
                zip = "67203",
                phone = ""
            };
            Assert.IsFalse(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );


            //Additional details: person who got married and/or divorced recently (hyphenated last name change)
            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Joann",
                lastname = "Sample-Testpatient",
                dob = "1970-04-01",
                gender = "Female",
                street = "123 Clementine",
                city = "Cincinnati",
                state = "OH",
                zip = "43215",
                phone = "513-555-3434"
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );


            //No prescriptions
            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Nobody",
                lastname = "Testpatient",
                dob = "1970-01-01",
                gender = "Male",
                street = "234 Street Three",
                city = "Home",
                state = "OH",
                zip = "43215",
                phone = "614-555-5678"
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );



            //Multiple prescriptions
            //person is NOT consolidated with Sherri Married (below)
            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Sherri",
                lastname = "Maiden",
                dob = "1970-05-01",
                gender = "Female",
                street = "456 Snickers Street",
                city = "Hershey",
                state = "OH",
                zip = "43215",
                phone = "513-555-4545"
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );


            //No prescriptions
            //results in a “Multiple Patients” response; results will NOT be provided
            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Sherri",
                lastname = "Married",
                dob = "1970-05-01",
                gender = "Female",
                street = "456 Snickers Street",
                city = "Hershey",
                state = "OH",
                zip = "43215",
                phone = "513-555-4545"
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => true,
                    (why) => false,
                    (why) => false)
                );


            //Multiple prescriptions
            patient = new HttpClientPMPGateway.Patient
            {
                firstname = "Steven",
                lastname = "Testpatient",
                dob = "1970-03-01",
                gender = "Male",
                street = "123 Fone",
                city = "Home",
                state = "OH",
                zip = "43215",
                phone = "614-555-1234"
            };
            Assert.IsTrue(
                await GetPatientReportAsync(patient,
                    (report) => true,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false,
                    (why) => false)
                );

        }

        private async Task<bool> GetReportAsync(HttpClientPMPGateway.Patient patient)
        {
            var pmpGateway = new HttpClientPMPGateway(baseUri, apiVersion, pmpCertificate, certificatePassword);
            return await await pmpGateway.PostPatientAsync<Task<bool>>(username, password, provider, patient,
                async (xmlDocument) =>
                {
                    var reportLinkNode = xmlDocument.Descendants().Where(x => x.Name.LocalName == "ViewableReport").FirstOrDefault();
                    if (null == reportLinkNode)
                        Assert.Fail();

                    return await pmpGateway.PostReportAsync(username, password, provider, reportLinkNode.Value,
                        (d) => true,
                        (d) => false,
                        (d) => false,
                        (d) => false,
                        (d) => false,
                        (d) => false);
                },
                (why) => false.ToTask(),
                (why) => false.ToTask(),
                (why) => false.ToTask(),
                (why) => false.ToTask(),
                (why) => false.ToTask(),
                (why) => false.ToTask(),
                (why) => false.ToTask());
        }

        private async Task<TResult> GetPatientReportAsync<TResult>(HttpClientPMPGateway.Patient patient,
            Func<HtmlAgilityPack.HtmlDocument, TResult> onSuccess,
            Func<string, TResult> onBadRequest,
            Func<string, TResult> onUnauthorized,
            Func<string, TResult> onNotFound,
            Func<string, TResult> onInternalServerError,
            Func<string, TResult> onCouldNotIdentifyUniquePatient,
            Func<string, TResult> onPMPError,
            Func<string, TResult> onFailure)
        {
            var pmpGateway = new HttpClientPMPGateway(baseUri, apiVersion, pmpCertificate, certificatePassword);
            return await pmpGateway.PostPatientReportAsync(username, password, provider, patient,
                (htmlDocument) =>
                {
                    Assert.IsTrue(htmlDocument.ParseErrors.Count() == 0);
                    return onSuccess(htmlDocument);
                },
                onBadRequest,
                onUnauthorized,
                onNotFound,
                onInternalServerError,
                onCouldNotIdentifyUniquePatient,
                onPMPError,
                onFailure);
        }
    }
}