using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Lecture;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.Regroupement;
using Xunit.Abstractions;

namespace ScanTrad.PipelineTests.Regroupement
{
    /// <summary>
    /// Enchaîne la lecture et le regroupement sur une vraie planche, pour vérifier
    /// que les deux étapes s'emboîtent sur des données que personne n'a arrangées.
    /// </summary>
    /// <remarks>
    /// Les tests unitaires du regroupement travaillent sur des rectangles écrits à la
    /// main : ils vérifient la logique, pas la rencontre avec le réel. Ici les zones
    /// viennent d'un moteur, avec ses approximations et ses ratés.
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class RegroupementSurPlancheReelleTests
    {
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec le collecteur de sortie fourni par xUnit.
        /// </summary>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public RegroupementSurPlancheReelleTests(ITestOutputHelper sortie)
        {
            this.sortie = sortie;
        }

        /// <summary>
        /// Sur une planche réelle, le regroupement réduit le nombre de zones sans
        /// jamais perdre de texte, et reconstitue des phrases entières.
        /// </summary>
        [Fact]
        public async Task Regrouper_ApresUneLectureReelle_ReconstitueDesPhrases()
        {
            byte[] image = await PlancheDEssai.ChargerAsync();

            IReadOnlyList<ZoneDeTexte> lignes;

            using (ILecteurDePage lecteur =
                new LecteurDePageComicTextDetector(PlancheDEssai.TrouverLeModele()))
            {
                lignes = await lecteur.LireAsync(image);
            }

            IReadOnlyList<ZoneDeTexte> blocs = new RegroupeurDeZones().Regrouper(lignes);

            Assert.NotEmpty(blocs);
            Assert.True(
                blocs.Count < lignes.Count,
                $"Le regroupement n'a rien rassemblé : {lignes.Count} lignes, {blocs.Count} blocs.");

            // L'invariant qui compte : rassembler ne doit rien perdre en chemin.
            foreach (ZoneDeTexte ligne in lignes)
            {
                Assert.Contains(blocs, bloc => bloc.TexteOriginal.Contains(ligne.TexteOriginal));
            }

            Assert.Contains(blocs, bloc => bloc.TexteOriginal.Contains(' '));

            Decrire(lignes, blocs);
        }

        private void Decrire(IReadOnlyList<ZoneDeTexte> lignes, IReadOnlyList<ZoneDeTexte> blocs)
        {
            sortie.WriteLine($"Planche    : {PlancheDEssai.Nom}");
            sortie.WriteLine($"Lignes lues: {lignes.Count}");
            sortie.WriteLine($"Blocs      : {blocs.Count}");
            sortie.WriteLine($"Avec bulle : {blocs.Count(bloc => bloc.Bulle != null)} sur {blocs.Count}");
            sortie.WriteLine(string.Empty);

            foreach (ZoneDeTexte bloc in blocs.OrderBy(bloc => bloc.Quadrilatere.Centre.Y))
            {
                string bulle = bloc.Bulle == null ? "sans bulle" : "en bulle";

                sortie.WriteLine($"[{bloc.Confiance:0.00}] ({bulle}) {bloc.TexteOriginal}");
            }
        }
    }
}
