using System.Runtime.Versioning;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Effacement;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.OrdreDeLecture;
using ScanTrad.Pipeline.Reecriture;
using ScanTrad.Pipeline.Traduction;

namespace ScanTrad.PipelineTests.Reecriture
{
    /// <summary>
    /// Enchaîne lecture, ordre de lecture, traduction, effacement et réécriture sur
    /// une vraie planche, et attache l'image composée au test pour qu'un humain aille
    /// la regarder.
    /// </summary>
    /// <remarks>
    /// La traduction employée est une vraie traduction, pas un texte de démonstration
    /// écrit à la main : c'est la seule façon de voir si le rendu tient face à ce
    /// qu'un moteur produit réellement — longueur imprévisible, ponctuation, parfois
    /// une réplique qu'il n'a pas su traduire. Un texte inventé aurait aussi fini par
    /// se répéter d'une bulle à l'autre, faute d'autant de phrases que de bulles.
    /// <para>
    /// Le moteur est un paramètre, comme pour <c>TraductionSurPlancheReelleTests</c> :
    /// l'explorateur affiche <c>(moteur: OpusMt)</c> et <c>(moteur: Nllb)</c>, et on
    /// ouvre directement l'image du moteur qu'on veut juger.
    /// </para>
    /// <para>
    /// Comme pour l'effacement, aucune assertion ne peut dire si un rendu est
    /// <em>joli</em> : le test vérifie le strict minimum et son vrai produit est
    /// l'image jointe.
    /// </para>
    /// <para>
    /// Sans les modèles de traduction locaux, ce test échoue avec un
    /// <see cref="System.IO.FileNotFoundException"/> explicite — c'est le même
    /// comportement que <c>TraductionSurPlancheReelleTests</c>, voir le README.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    [SupportedOSPlatform("windows")]
    public class ReecritureSurPlancheReelleTests
    {
        private readonly LectureDeLaPlancheDEssai lecture;
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec la lecture partagée et le collecteur de sortie.
        /// </summary>
        /// <param name="lecture">La planche d'essai, déjà lue.</param>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public ReecritureSurPlancheReelleTests(LectureDeLaPlancheDEssai lecture, ITestOutputHelper sortie)
        {
            this.lecture = lecture;
            this.sortie = sortie;
        }

        /// <summary>
        /// Traduit, efface puis réécrit les bulles de la planche d'essai, attache
        /// l'image composée et la dépose aussi à côté du binaire de test.
        /// </summary>
        /// <param name="moteur">Le moteur de traduction dont on juge le rendu.</param>
        [Theory]
        [InlineData(MoteurDeTraduction.OpusMt)]
        [InlineData(MoteurDeTraduction.Nllb)]
        public async Task Reecrire_ApresTraduction_DonneLImageComposeeAVoir(MoteurDeTraduction moteur)
        {
            // Cette édition est retournée : elle se lit de gauche à droite.
            Planche ordonnee = new OrdonnanceurDeZones().Ordonner(lecture.Copier(SensDeLecture.GaucheADroite));

            using ITraducteur traducteur = new ChoixDuTraducteur().Creer(moteur);

            Planche traduite = await traducteur.TraduireAsync(
                ordonnee, TestContext.Current.CancellationToken);

            Planche nettoyee = new EffaceurParRemplissage().Effacer(traduite);
            Planche composee = new ReecrivainParAjustement().Reecrire(nettoyee);

            int avecTraduction = traduite.Zones
                .Count(zone => zone.Bulle != null && !string.IsNullOrEmpty(zone.TexteTraduit));

            Assert.NotEqual(nettoyee.Image.Length, composee.Image.Length);
            Assert.True(avecTraduction > 0, "Aucune zone traduite : le test ne prouverait rien.");

            TestContext.Current.AddAttachment($"apercu-reecriture-{moteur}", composee.Image, "image/png");

            string fichier = Deposer(composee, $"Akashic-composee-{moteur}.png");

            sortie.WriteLine($"Moteur            : {moteur}");
            sortie.WriteLine($"Blocs             : {traduite.Zones.Count}");
            sortie.WriteLine($"Bulles réécrites  : {avecTraduction}");
            sortie.WriteLine($"Poids composé     : {composee.Image.Length / 1024} Ko");
            sortie.WriteLine(string.Empty);

            foreach (ZoneDeTexte zone in traduite.Zones.OrderBy(zone => zone.OrdreDeLecture))
            {
                sortie.WriteLine($"{zone.OrdreDeLecture,2}. EN : {zone.TexteOriginal}");
                sortie.WriteLine($"    FR : {zone.TexteTraduit ?? "(non traduit)"}");
            }

            sortie.WriteLine(string.Empty);
            sortie.WriteLine("Image composée jointe au test, et déposée ici :");
            sortie.WriteLine(fichier);
        }

        private static string Deposer(Planche planche, string nom)
        {
            string dossier = Path.Combine(AppContext.BaseDirectory, "sorties");

            Directory.CreateDirectory(dossier);

            string fichier = Path.Combine(dossier, nom);

            File.WriteAllBytes(fichier, planche.Image);

            return fichier;
        }
    }
}
