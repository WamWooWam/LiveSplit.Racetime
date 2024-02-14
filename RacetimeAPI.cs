using CefSharp;
using CefSharp.WinForms;
using LiveSplit.Model;
using LiveSplit.Racetime.Controller;
using LiveSplit.Racetime.Model;
using LiveSplit.Racetime.View;
using LiveSplit.UI.Components;
using LiveSplit.Web;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiveSplit.Racetime
{
    public class RacetimeAPI : RaceProviderAPI
    {
        protected static readonly Uri BaseUri = new Uri($"{Properties.Resources.PROTOCOL_REST}://{Properties.Resources.DOMAIN}/");
        protected static string racesEndpoint => Properties.Resources.ENDPOINT_RACES;
        private static RacetimeAPI _instance;
        public static RacetimeAPI Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new RacetimeAPI();
                return _instance;
            }
        }


        public RacetimeAPI()
        {
            Authenticator = new RacetimeAuthenticator(new RTAuthentificationSettings());
            JoinRace = Join;
            CreateRace = Create;
        }

        public void Join(ITimerModel model, string id)
        {
            var channel = new RacetimeChannel(model.CurrentState, model, (RacetimeSettings)Settings);
            _ = new ChannelForm(channel, id, model.CurrentState.LayoutSettings.AlwaysOnTop);
        }

        public void Warn()
        {

        }

        public void Create(ITimerModel model)
        {
            Process.Start(GetUri(Properties.Resources.CREATE_RACE_ADDRESS).AbsoluteUri);
        }

        public IEnumerable<Race> Races { get; set; }

        internal RacetimeAuthenticator Authenticator { get; set; }

        public override string ProviderName => "racetime.gg";

        public override string Username => Authenticator.Identity?.Name;

        protected Uri GetUri(string subUri)
        {
            return new Uri(BaseUri, subUri);
        }

        public override void RefreshRacesListAsync()
        {
            Task.Factory.StartNew(() => RefreshRacesList());
        }

        protected void RefreshRacesList()
        {
            try
            {
                Races = GetRacesFromServer().ToArray();
                RacesRefreshedCallback?.Invoke(this);
            }
            catch { }
        }


        protected IEnumerable<Race> GetRacesFromServer()
        {
            var request = WebRequest.Create(new Uri(BaseUri.AbsoluteUri + racesEndpoint));
            request.Headers.Add("Authorization", "Bearer " + Authenticator.AccessToken);

            using (var response = request.GetResponse())
            {
                var data = JSON.FromResponse(response);

                var races = data.races;
                foreach (var r in races)
                {
                    Race raceObj;
                    r.entrants = new List<dynamic>();
                    raceObj = RTModelBase.Create<Race>(r);
                    yield return raceObj;
                }
                yield break;
            }
        }

        public override IEnumerable<IRaceInfo> GetRaces()
        {
            return Races;
        }

        Dictionary<string, Image> CategoryImagesCache = new Dictionary<string, Image>();
        public override Image GetGameImage(string id)
        {
            try
            {
                foreach (var race in Races)
                {
                    if (race.Data.category.slug == id)
                    {
                        if (CategoryImagesCache.ContainsKey(id))
                        {
                            return CategoryImagesCache[id];
                        }
                        else if (race.Data.category.image != null)
                        {
                            WebClient wc = new WebClient();
                            byte[] bytes = wc.DownloadData(race.Data.category.image);
                            MemoryStream ms = new MemoryStream(bytes);
                            System.Drawing.Image img = System.Drawing.Image.FromStream(ms);
                            CategoryImagesCache.Add(id, img);
                            return img;
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
