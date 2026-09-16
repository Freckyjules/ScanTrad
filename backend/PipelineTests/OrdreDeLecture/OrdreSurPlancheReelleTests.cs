using ScanTrad.Pipeline.Cadrage;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.OrdreDeLecture;

namespace ScanTrad.PipelineTests.OrdreDeLecture
{
    /// <summary>
    /// Met l'ordre de lecture à l'épreuve sur une vraie planche, et affiche le
    /// dialogue obtenu.
    /// </summary>
    /// <remarks>
    /// La lecture ne se refait pas ici : elle vient de
    /// <see cref="LectureDeLaPlancheDEssai"/>, partagée par toute la collection
    /// d'intégration. Le cadrage et l'ordonnancement, eux, ne sont que de la
    /// géométrie — ils ne coûtent rien et n'ont pas besoin de l'image.
    /// <para>
    /// Chaque cas travaille sur une <see cref="LectureDeLaPlancheDEssai.Copier"/> et
    /// non sur les zones partagées, parce que l'ordonnanceur écrit le rang dans les
    /// zones qu'on lui donne. Les deux sens s'exécuteraient sinon l'un sur le
    /// résultat de l'autre.
    /// </para>
    /// <para>
    /// Le test ne fige aucun ordre précis. Il vérifie les invariants du rang : chaque
    /// bloc en a un, ils vont de zéro à n-1, et aucun n'est en double. Attendre une
    /// suite de répliques exacte reviendrait à figer le comportement des modèles, qui
    /// changera à la prochaine version. Ce que l'ordre vaut vraiment se juge sur
    /// l'aperçu, où le chemin de lecture est tracé d'une bulle à l'autre.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class OrdreSurPlancheReelleTests
    {
        private readonly LectureDeLaPlancheDEssai lecture;
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec la lecture partagée et le collecteur de sortie.
        /// </summary>
        /// <param name="lecture">La planche d'essai, déjà lue.</param>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public OrdreSurPlancheReelleTests(LectureDeLaPlancheDEssai lecture, ITestOutputHelper sortie)
        {
            this.lecture = lecture;
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
        public void Ordonner_ApresUneLecture_DonneUnRangUniqueAChaqueBloc(SensDeLecture sens)
        {
            Planche lue = lecture.Copier(sens);

            // Le cadrage vient avant l'ordre dans le pipeline réel : l'ordonnanceur
            // trie sur le centre du rectangle, et c'est ce centre que le cadrage peut
            // déplacer. Le sauter donnerait un aperçu qui ne montre pas ce que
            // l'ordonnanceur voit vraiment.
            Planche cadree = new AjusteurDeRectangleParRasterisation().Ajuster(lue);

            // Le sens de lecture vient de la planche, pas d'un réglage de
            // l'ordonnanceur : la même instance traite les deux sens.
            Planche ordonnee = new OrdonnanceurDeZones().Ordonner(cadree);
            IReadOnlyList<ZoneDeTexte> ordre = ordonnee.Zones;

            Assert.Equal(lue.Zones.Count, ordre.Count);

            // Les rangs forment exactement la suite 0, 1, 2… sans trou ni doublon.
            Assert.Equal(
                Enumerable.Range(0, ordre.Count),
                ordre.Select(bloc => bloc.OrdreDeLecture!.Value));

            ApercuDePlanche.Attacher($"apercu-ordre-{sens}", ordonnee);

            Decrire(sens, ordre);
        }

        private void Decrire(SensDeLecture sens, IReadOnlyList<ZoneDeTexte> ordre)
        {
            sortie.WriteLine($"Planche : {PlancheDEssai.Nom}");
            sortie.WriteLine($"Sens    : {sens}");
            sortie.WriteLine(string.Empty);
            sortie.WriteLine("Aperçu attaché : le chemin magenta suit l'ordre de bloc en bloc.");
            sortie.WriteLine(string.Empty);

            foreach (ZoneDeTexte bloc in ordre)
            {
                sortie.WriteLine($"{bloc.OrdreDeLecture,2}. {bloc.TexteOriginal}");
            }
        }
    }
}
