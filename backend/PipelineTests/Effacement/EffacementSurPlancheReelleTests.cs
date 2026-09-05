using ScanTrad.Pipeline.Effacement;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Effacement
{
    /// <summary>
    /// Efface le texte d'une vraie planche et attache l'image nettoyée au test, pour
    /// qu'un humain aille la regarder.
    /// </summary>
    /// <remarks>
    /// Ce n'est pas vraiment un test : aucune assertion ne peut dire si un effacement
    /// est « joli ». Il vérifie le strict minimum — l'image a changé, l'originale est
    /// intacte, il y avait bien des bulles à effacer — et son vrai produit est l'image
    /// jointe.
    /// <para>
    /// Elle est attachée telle quelle, sans rien dessiner dessus : ce qu'on veut voir
    /// ici, c'est le résultat livrable, pas ce que la détection a trouvé. Les cadres
    /// et les contours sont l'affaire de l'aperçu de lecture.
    /// </para>
    /// <para>
    /// La lecture vient du partage de la collection : elle coûte une vingtaine de
    /// secondes et rend le même résultat pour tout le monde. L'effacement, lui, ne
    /// travaille que sur des pixels.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class EffacementSurPlancheReelleTests
    {
        private readonly LectureDeLaPlancheDEssai lecture;
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec la lecture partagée et le collecteur de sortie.
        /// </summary>
        /// <param name="lecture">La planche d'essai, déjà lue.</param>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public EffacementSurPlancheReelleTests(LectureDeLaPlancheDEssai lecture, ITestOutputHelper sortie)
        {
            this.lecture = lecture;
            this.sortie = sortie;
        }

        /// <summary>
        /// Efface les bulles de la planche d'essai, attache l'image nettoyée et la
        /// dépose aussi à côté du binaire de test.
        /// </summary>
        [Fact]
        public void Effacer_SurUnePlancheReelle_DonneLImageNettoyeeAVoir()
        {
            // Une copie, et non les zones partagées : l'effacement ne les modifie pas
            // aujourd'hui, mais rien ne le garantit, et les autres tests s'appuient
            // dessus.
            Planche lue = lecture.Copier(SensDeLecture.GaucheADroite);

            Planche nettoyee = new EffaceurParRemplissage().Effacer(lue);

            int avecBulle = lue.Zones.Count(zone => zone.Bulle != null);

            Assert.NotEqual(lue.Image.Length, nettoyee.Image.Length);
            Assert.Equal(lecture.Originale.Image, lue.Image);
            Assert.True(avecBulle > 0, "Aucune bulle à effacer : le test ne prouverait rien.");

            TestContext.Current.AddAttachment("apercu-effacement", nettoyee.Image, "image/png");

            string fichier = Deposer(nettoyee, "Akashic-nettoyee.png");

            sortie.WriteLine($"Blocs           : {lue.Zones.Count}");
            sortie.WriteLine($"Bulles effacées : {avecBulle}");
            sortie.WriteLine($"Poids d'origine : {lue.Image.Length / 1024} Ko");
            sortie.WriteLine($"Poids nettoyé   : {nettoyee.Image.Length / 1024} Ko");
            sortie.WriteLine(string.Empty);
            sortie.WriteLine("Image nettoyée jointe au test, et déposée ici :");
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
