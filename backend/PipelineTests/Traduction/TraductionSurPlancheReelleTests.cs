using System.Diagnostics;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.OrdreDeLecture;
using ScanTrad.Pipeline.Traduction;

namespace ScanTrad.PipelineTests.Traduction
{
    /// <summary>
    /// Enchaîne lecture, ordre de lecture et traduction sur une vraie planche, et
    /// affiche le dialogue français obtenu — une fois par moteur.
    /// </summary>
    /// <remarks>
    /// Ce n'est pas vraiment un test : aucune assertion ne peut dire si une traduction
    /// est bonne. Son produit est la sortie, faite pour être lue par un humain — c'est
    /// le seul endroit où l'on voit ce que la chaîne complète donne réellement, et le
    /// seul moyen de comparer deux moteurs sur la même planche.
    /// <para>
    /// Le moteur est un paramètre, donc son nom apparaît dans le nom du test :
    /// l'explorateur affiche <c>(moteur: OpusMt)</c> et <c>(moteur: Nllb)</c>, et on
    /// ouvre directement celui qu'on veut lire.
    /// </para>
    /// <para>
    /// La seule vérification est qu'au moins une zone est ressortie traduite. Sans
    /// elle, une chaîne cassée passerait au vert en n'affichant rien, ce qui serait
    /// pire qu'un échec.
    /// </para>
    /// <para>
    /// La lecture vient du partage de la collection ; l'ordre et la traduction se
    /// refont ici, sur une copie, pour que ce test n'influe sur aucun autre.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class TraductionSurPlancheReelleTests
    {
        private readonly LectureDeLaPlancheDEssai lecture;
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec la lecture partagée et le collecteur de sortie.
        /// </summary>
        /// <param name="lecture">La planche d'essai, déjà lue.</param>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public TraductionSurPlancheReelleTests(
            LectureDeLaPlancheDEssai lecture, ITestOutputHelper sortie)
        {
            this.lecture = lecture;
            this.sortie = sortie;
        }

        /// <summary>
        /// Lit, ordonne, traduit, et affiche le dialogue anglais face au français.
        /// </summary>
        /// <param name="moteur">Le moteur de traduction mis à l'épreuve.</param>
        [Theory]
        [InlineData(MoteurDeTraduction.OpusMt)]
        [InlineData(MoteurDeTraduction.Nllb)]
        public async Task Traduire_ApresLectureEtOrdre_AfficheLeDialogue(MoteurDeTraduction moteur)
        {
            // Cette édition est retournée : elle se lit de gauche à droite.
            Planche ordonnee = new OrdonnanceurDeZones()
                .Ordonner(lecture.Copier(SensDeLecture.GaucheADroite));

            Stopwatch chrono = Stopwatch.StartNew();

            using ITraducteur traducteur = new ChoixDuTraducteur().Creer(moteur);

            long chargement = chrono.ElapsedMilliseconds;
            chrono.Restart();

            Planche traduite = await traducteur.TraduireAsync(
                ordonnee, TestContext.Current.CancellationToken);

            chrono.Stop();

            Decrire(moteur, traduite, chargement, chrono.ElapsedMilliseconds);

            Assert.Contains(traduite.Zones, zone => !string.IsNullOrWhiteSpace(zone.TexteTraduit));
        }

        private void Decrire(
            MoteurDeTraduction moteur, Planche traduite, long chargement, long traduction)
        {
            IReadOnlyList<ZoneDeTexte> parOrdre = traduite.Zones
                .OrderBy(zone => zone.OrdreDeLecture)
                .ToList();

            int traduites = parOrdre.Count(zone => !string.IsNullOrWhiteSpace(zone.TexteTraduit));

            sortie.WriteLine($"Moteur       : {moteur}");
            sortie.WriteLine($"Planche      : {PlancheDEssai.Nom}");
            sortie.WriteLine($"Blocs        : {parOrdre.Count}, dont {traduites} traduits");
            sortie.WriteLine($"Chargement   : {chargement} ms");
            sortie.WriteLine($"Traduction   : {traduction} ms " +
                             $"({traduction / Math.Max(1, traduites)} ms par bloc)");
            sortie.WriteLine(string.Empty);

            foreach (ZoneDeTexte zone in parOrdre)
            {
                sortie.WriteLine($"{zone.OrdreDeLecture,2}. EN : {zone.TexteOriginal}");
                sortie.WriteLine($"    FR : {zone.TexteTraduit ?? "(non traduit)"}");
                sortie.WriteLine(string.Empty);
            }
        }
    }
}
