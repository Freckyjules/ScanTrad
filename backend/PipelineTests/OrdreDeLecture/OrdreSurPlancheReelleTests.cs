using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Lecture;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.OrdreDeLecture;

namespace ScanTrad.PipelineTests.OrdreDeLecture
{
    /// <summary>
    /// Enchaîne la lecture et l'ordre de lecture sur une vraie
    /// planche, et affiche le dialogue obtenu.
    /// </summary>
    /// <remarks>
    /// Le test ne fige aucun ordre précis. Il vérifie les invariants du rang : chaque
    /// bloc en a un, ils vont de zéro à n-1, et aucun n'est en double. Attendre une
    /// suite de répliques exacte reviendrait à figer le comportement des modèles, qui
    /// changera à la prochaine version.
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class OrdreSurPlancheReelleTests
    {
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec le collecteur de sortie fourni par xUnit.
        /// </summary>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public OrdreSurPlancheReelleTests(ITestOutputHelper sortie)
        {
            this.sortie = sortie;
        }

        /// <summary>
        /// Sur une planche réelle, chaque bloc reçoit un rang unique, et l'ordre
        /// obtenu se lit comme un dialogue.
        /// </summary>
        /// <param name="sens">Le sens de lecture mis à l'épreuve.</param>
        [Theory]
        [InlineData(SensDeLecture.DroiteAGauche)]
        [InlineData(SensDeLecture.GaucheADroite)]
        public async Task Ordonner_ApresUneLecture_DonneUnRangUniqueAChaqueBloc(
            SensDeLecture sens)
        {
            Planche planche = new Planche(await PlancheDEssai.ChargerAsync(), sens);

            Planche lue;

            using (ILecteurDePlanche lecteur =
                new LecteurDePlancheComicTextDetector(PlancheDEssai.TrouverLeModele()))
            {
                lue = await lecteur.LireAsync(planche);
            }

            // Le sens de lecture vient de la planche, pas d'un réglage de
            // l'ordonnanceur : la même instance traite les deux sens.
            IReadOnlyList<ZoneDeTexte> ordre =
                new OrdonnanceurParCoupeRecursive().Ordonner(lue).Zones;

            Assert.Equal(lue.Zones.Count, ordre.Count);

            // Les rangs forment exactement la suite 0, 1, 2… sans trou ni doublon.
            Assert.Equal(
                Enumerable.Range(0, ordre.Count),
                ordre.Select(bloc => bloc.OrdreDeLecture!.Value));

            Decrire(sens, ordre);
        }

        private void Decrire(SensDeLecture sens, IReadOnlyList<ZoneDeTexte> ordre)
        {
            sortie.WriteLine($"Planche : {PlancheDEssai.Nom}");
            sortie.WriteLine($"Sens    : {sens}");
            sortie.WriteLine(string.Empty);

            foreach (ZoneDeTexte bloc in ordre)
            {
                sortie.WriteLine($"{bloc.OrdreDeLecture,2}. {bloc.TexteOriginal}");
            }
        }
    }
}
