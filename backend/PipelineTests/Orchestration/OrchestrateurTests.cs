using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.Orchestration;

namespace ScanTrad.PipelineTests.Orchestration
{
    /// <summary>
    /// Vérifie l'enchaînement des six étapes avec des étapes factices : aucun modèle
    /// n'est chargé, le test s'exécute en millisecondes.
    /// </summary>
    /// <remarks>
    /// Chaque étape factice rend une planche neuve et reconnaissable ; le test vérifie
    /// à la fois l'ordre des appels et que chaque étape reçoit bien la planche que la
    /// précédente a rendue, pas la planche de départ ni une autre. Que les vraies
    /// étapes s'enchaînent correctement sur une vraie planche est vérifié par le test
    /// d'intégration, qui passe par les mêmes six interfaces.
    /// <para>
    /// Une seule classe factice implémente les six interfaces à la fois : chaque
    /// instance ne joue qu'un rôle (celui du paramètre où elle est passée), mais ça
    /// évite d'écrire six classes quasi identiques.
    /// </para>
    /// </remarks>
    public class OrchestrateurTests
    {
        /// <summary>
        /// Les six étapes s'exécutent dans l'ordre du pipeline.
        /// </summary>
        [Fact]
        public async Task TraiterAsync_ExecuteLesEtapesDansLOrdre()
        {
            List<string> journal = new List<string>();
            Orchestrateur orchestrateur = Construire(journal, out Planche depart, out _, out _);

            await orchestrateur.TraiterAsync(depart, TestContext.Current.CancellationToken);

            Assert.Equal(
                new[] { "lecture", "cadrage", "ordre", "traduction", "effacement", "reecriture" }, journal);
        }

        /// <summary>
        /// Chaque étape reçoit la planche que la précédente a rendue, jamais une autre.
        /// </summary>
        [Fact]
        public async Task TraiterAsync_ChaineLaPlancheDUneEtapeALaSuivante()
        {
            List<string> journal = new List<string>();

            Orchestrateur orchestrateur = Construire(
                journal, out Planche depart, out Dictionary<string, Planche> recues, out Dictionary<string, Planche> rendues);

            Planche resultat = await orchestrateur.TraiterAsync(depart, TestContext.Current.CancellationToken);

            Assert.Same(depart, recues["lecture"]);
            Assert.Same(rendues["lecture"], recues["cadrage"]);
            Assert.Same(rendues["cadrage"], recues["ordre"]);
            Assert.Same(rendues["ordre"], recues["traduction"]);
            Assert.Same(rendues["traduction"], recues["effacement"]);
            Assert.Same(rendues["effacement"], recues["reecriture"]);
            Assert.Same(rendues["reecriture"], resultat);
        }

        /// <summary>
        /// Traiter rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public async Task TraiterAsync_Null_LeveArgumentNullException()
        {
            IOrchestrateurDePipeline orchestrateur = Construire(new List<string>(), out _, out _, out _);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => orchestrateur.TraiterAsync(null!, TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// Une étape reçue à <c>null</c> n'a pas de sens et doit être signalée.
        /// </summary>
        [Fact]
        public void Constructeur_UneEtapeNulle_LeveArgumentNullException()
        {
            EtapeFactice etape = new EtapeFactice("etape", new List<string>(), new(), new());

            Assert.Throws<ArgumentNullException>(
                () => new Orchestrateur(null!, etape, etape, etape, etape, etape));
        }

        private static Orchestrateur Construire(
            List<string> journal,
            out Planche depart,
            out Dictionary<string, Planche> recues,
            out Dictionary<string, Planche> rendues)
        {
            depart = new Planche(new byte[] { 0 });
            recues = new Dictionary<string, Planche>();
            rendues = new Dictionary<string, Planche>();

            EtapeFactice lecture = new EtapeFactice("lecture", journal, recues, rendues);
            EtapeFactice cadrage = new EtapeFactice("cadrage", journal, recues, rendues);
            EtapeFactice ordre = new EtapeFactice("ordre", journal, recues, rendues);
            EtapeFactice traduction = new EtapeFactice("traduction", journal, recues, rendues);
            EtapeFactice effacement = new EtapeFactice("effacement", journal, recues, rendues);
            EtapeFactice reecriture = new EtapeFactice("reecriture", journal, recues, rendues);

            return new Orchestrateur(lecture, cadrage, ordre, traduction, effacement, reecriture);
        }

        /// <summary>
        /// Joue les six rôles du pipeline à la fois : chaque instance n'en tient qu'un,
        /// celui du paramètre du constructeur d'<see cref="Orchestrateur"/> où elle est
        /// passée. Consigne son nom dans un journal partagé et rend une planche neuve,
        /// pour que le test vérifie l'ordre des appels et le câblage sans dupliquer six
        /// classes presque identiques.
        /// </summary>
        private class EtapeFactice :
            ILecteurDePlanche, IAjusteurDeRectangle, IOrdonnanceurDeZones, ITraducteur, IEffaceurDeTexte,
            IReecrivainDeTexte
        {
            private readonly string nom;
            private readonly List<string> journal;
            private readonly Dictionary<string, Planche> recues;
            private readonly Dictionary<string, Planche> rendues;

            public EtapeFactice(
                string nom, List<string> journal, Dictionary<string, Planche> recues, Dictionary<string, Planche> rendues)
            {
                this.nom = nom;
                this.journal = journal;
                this.recues = recues;
                this.rendues = rendues;
            }

            public Task<Planche> LireAsync(Planche planche, CancellationToken jetonAnnulation = default)
            {
                return Task.FromResult(Executer(planche));
            }

            public Planche Ajuster(Planche planche)
            {
                return Executer(planche);
            }

            public Planche Ordonner(Planche planche)
            {
                return Executer(planche);
            }

            public Task<Planche> TraduireAsync(Planche planche, CancellationToken jetonAnnulation = default)
            {
                return Task.FromResult(Executer(planche));
            }

            public Planche Effacer(Planche planche)
            {
                return Executer(planche);
            }

            public Planche Reecrire(Planche planche)
            {
                return Executer(planche);
            }

            public void Dispose()
            {
            }

            private Planche Executer(Planche planche)
            {
                journal.Add(nom);
                recues[nom] = planche;

                Planche rendue = new Planche(new byte[] { 0 });
                rendues[nom] = rendue;

                return rendue;
            }
        }
    }
}
