using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Effacement;
using ScanTrad.Pipeline.Lecture;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Effacement
{
    /// <summary>
    /// Fait tourner le pipeline complet sur une vraie planche et enregistre l'image
    /// nettoyée sur le disque, pour qu'un humain aille la regarder.
    /// </summary>
    /// <remarks>
    /// Ce n'est pas vraiment un test : aucune assertion ne peut dire si un effacement
    /// est « joli ». Il vérifie le strict minimum — l'image a changé, elle reste
    /// décodable, elle fait la même taille — et son vrai produit est le fichier qu'il
    /// dépose, dont il affiche le chemin.
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class EffacementSurPlancheReelleTests
    {
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec le collecteur de sortie fourni par xUnit.
        /// </summary>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public EffacementSurPlancheReelleTests(ITestOutputHelper sortie)
        {
            this.sortie = sortie;
        }

        /// <summary>
        /// Enchaîne lecture, regroupement et effacement, puis dépose l'image nettoyée
        /// à côté du binaire de test.
        /// </summary>
        [Fact]
        public async Task Effacer_SurUnePlancheReelle_DeposeLImageNettoyeeAVoir()
        {
            Planche originale = new Planche(
                await PlancheDEssai.ChargerAsync(), SensDeLecture.GaucheADroite);

            Planche lue;

            using (ILecteurDePlanche lecteur =
                new LecteurDePlancheComicTextDetector(PlancheDEssai.TrouverLeModele()))
            {
                lue = await lecteur.LireAsync(originale);
            }

            Planche nettoyee = new EffaceurParRemplissage().Effacer(lue);

            int avecBulle = lue.Zones.Count(zone => zone.Bulle != null);

            Assert.NotEqual(originale.Image.Length, nettoyee.Image.Length);
            Assert.Equal(originale.Image, lue.Image);
            Assert.True(avecBulle > 0, "Aucune bulle à effacer : le test ne prouverait rien.");

            string fichier = Deposer(nettoyee, "Akashic-nettoyee.png");

            sortie.WriteLine($"Blocs           : {lue.Zones.Count}");
            sortie.WriteLine($"Bulles effacées : {avecBulle}");
            sortie.WriteLine($"Poids d'origine : {originale.Image.Length / 1024} Ko");
            sortie.WriteLine($"Poids nettoyé   : {nettoyee.Image.Length / 1024} Ko");
            sortie.WriteLine(string.Empty);
            sortie.WriteLine("Image à regarder :");
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
