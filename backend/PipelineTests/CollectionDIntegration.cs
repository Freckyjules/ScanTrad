namespace ScanTrad.PipelineTests
{
    /// <summary>
    /// Regroupe les tests d'intégration pour qu'ils s'exécutent les uns après les
    /// autres, jamais en parallèle, et leur partage une unique lecture de la planche
    /// d'essai.
    /// </summary>
    /// <remarks>
    /// Chacun construit un moteur PaddleOCR et une session ONNX, qui allouent de la
    /// mémoire native hors de portée du ramasse-miettes. Plusieurs moteurs
    /// instanciés en même temps font tomber le processus entier sur une
    /// <c>AccessViolationException</c> — et l'hôte de test emporte avec lui les
    /// résultats des autres tests, ce qui rend le diagnostic déroutant.
    /// <para>
    /// Toute nouvelle classe de test qui charge un modèle doit porter
    /// <c>[Collection(Nom)]</c>.
    /// </para>
    /// <para>
    /// Le partage de <see cref="LectureDeLaPlancheDEssai"/> est déclaré ici, sur la
    /// collection, et non classe par classe : la lecture coûte une vingtaine de
    /// secondes et rend le même résultat pour tout le monde. Une classe de la
    /// collection y accède en déclarant simplement le type dans son constructeur.
    /// </para>
    /// </remarks>
    [CollectionDefinition(Nom, DisableParallelization = true)]
    public class CollectionDIntegration : ICollectionFixture<LectureDeLaPlancheDEssai>
    {
        /// <summary>
        /// Nom de la collection, à reprendre dans l'attribut <c>[Collection]</c> de
        /// chaque classe concernée.
        /// </summary>
        public const string Nom = "Integration";
    }
}
