using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CustomLogger;
using CyberBackendLibrary.HTTP;
using WebAPIService.NDREAMS.Aurora;
using WebAPIService.NDREAMS.BlueprintHome;
using WebAPIService.NDREAMS.Fubar;

namespace WebAPIService.NDREAMS
{
    public class NDREAMSClass : IDisposable
    {
        private bool disposedValue;
        private string absolutepath;
        private string baseurl;
        private string filepath;
        private string fullurl;
        private string apipath;
        private string method;
        private string host;

        public NDREAMSClass(string method, string filepath, string baseurl, string fullurl, string absolutepath, string apipath, string host)
        {
            this.absolutepath = absolutepath;
            this.filepath = filepath;
            this.baseurl = baseurl;
            this.fullurl = fullurl;
            this.method = method;
            this.apipath = apipath;
            this.host = host;
        }

        public string? ProcessRequest(Dictionary<string, string>? QueryParameters, byte[]? PostData = null, string? ContentType = null)
        {
            if (string.IsNullOrEmpty(absolutepath))
                return null;

            switch (method)
            {
                case "POST":
                    switch (absolutepath)
                    {
                        case "/fubar/fisi.php":
                            return fisi.fisiProcess(PostData, ContentType);
                        case "/Teaser/beans.php":
                            return Teaser.ProcessBeans(PostData, ContentType);
                        case "/aurora/visit.php":
                            return visit.ProcessVisit(PostData, ContentType, apipath);
                        case "/aurora/Blimp.php":
                            return Blimp.ProcessBlimps(PostData, ContentType);
                        case "/aurora/almanac.php":
                        case "/aurora/almanacWeights.php":
                            return Almanac.ProcessAlmanac(PostData, ContentType, fullurl, apipath);
                        case "/aurora/MysteryItems/mystery3.php":
                            return Mystery3.ProcessMystery3(PostData, ContentType, fullurl, apipath);
                        case "/aurora/VisitCounter2.php":
                            return AuroraDBManager.ProcessVisitCounter2(PostData, ContentType, apipath);
                        case "/aurora/TheEnd.php":
                            return AuroraDBManager.ProcessTheEnd(PostData, ContentType, apipath);
                        case "/aurora/OrbrunnerScores.php":
                            return AuroraDBManager.ProcessOrbrunnerScores(PostData, ContentType, apipath);
                        case "/aurora/Consumables.php":
                            return AuroraDBManager.ProcessConsumables(PostData, ContentType, apipath);
                        case "/aurora/PStats.php":
                            return AuroraDBManager.ProcessPStats(PostData, ContentType);
                        case "/aurora/ReleaseInfo.php":
                            return AuroraDBManager.ProcessReleaseInfo(PostData, ContentType, apipath);
                        case "/aurora/AuroraXP.php":
                            return AuroraDBManager.ProcessAuroraXP(PostData, ContentType, apipath);
                        case "/aurora/VRSignUp.php":
                            return VRSignUp.ProcessVRSignUp(PostData, ContentType, apipath);
                        case "/gateway/":
                            return "<xml></xml>"; // Not gonna emulate this encrypted mess.
                        case "/thecomplex/ComplexABTest.php":
                            return AuroraDBManager.ProcessComplexABTest(PostData, ContentType);
                        case "/blueprint/blueprint_furniture.php":
                            return Furniture.ProcessFurniture(PostData, ContentType, baseurl, apipath);
                        default:
                            LoggerAccessor.LogWarn($"[NDREAMS] - Unknown POST method: {absolutepath} was requested. Please report to GITHUB");
                            break;
                    }
                    break;
                case "GET":
                    {
                        if (host == "nDreams-multiserver-cdn")
                        {
                            if (File.Exists(filepath)) // We do some api filtering afterwards.
                            {
                                if (filepath.Contains("/NDREAMS/BlueprintHome/Layout/"))
                                {
                                    try
                                    {
                                        // Split the URL into segments
                                        string[] segments = filepath.Trim('/').Split('/');

                                        if (segments.Length == 5) // Url is effectively a Blueprint Home Furn/Layout fetch, so we update current used slot for each.
                                            File.WriteAllText(apipath + $"/NDREAMS/BlueprintHome/{segments[2]}/{segments[3]}/CurrentSlot.txt", segments[4][9..1]);
                                    }
                                    catch (Exception ex)
                                    {
                                        LoggerAccessor.LogError($"[NDREAMS] - Server thrown an exception while updating a BlueprintHome Current Slot: {ex}");
                                    }
                                }

                                return File.ReadAllText(filepath);
                            }
                            else
                                LoggerAccessor.LogWarn($"[NDREAMS] - Client requested a non-existant nDreams CDN file: {filepath}");
                        }
                        else
                            LoggerAccessor.LogWarn($"[NDREAMS] - Unknown GET method: {absolutepath} was requested. Please report to GITHUB");
                        break;
                    }
                default:
                    break;
            }

            return null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    absolutepath = string.Empty;
                    method = string.Empty;
                }

                // TODO: libérer les ressources non managées (objets non managés) et substituer le finaliseur
                // TODO: affecter aux grands champs une valeur null
                disposedValue = true;
            }
        }

        // // TODO: substituer le finaliseur uniquement si 'Dispose(bool disposing)' a du code pour libérer les ressources non managées
        // ~NDREAMSClass()
        // {
        //     // Ne changez pas ce code. Placez le code de nettoyage dans la méthode 'Dispose(bool disposing)'
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Ne changez pas ce code. Placez le code de nettoyage dans la méthode 'Dispose(bool disposing)'
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
