namespace ScanTrad.PipelineTests
{
    /// <summary>
    /// Retrouve l'export OPUS-MT dont se servent les tests de traduction.
    /// </summary>
    /// <remarks>
    /// Le modèle pèse plus d'un gigaoctet et n'est pas versionné : il se cherche en
    /// remontant l'arborescence, comme celui de la détection de blocs.
    /// </remarks>
    public static class ModeleOpusMt
    {
        private const string NomDuDossier = "opus-mt-en-fr";

        /// <summary>
        /// Retrouve le dossier de l'export en remontant depuis le dossier d'exécution.
        /// </summary>
        /// <returns>Le chemin complet du dossier du modèle.</returns>
        /// <exception cref="DirectoryNotFoundException">
        /// Levée si l'export n'a pas été déposé dans <c>backend/modeles/</c>.
        /// </exception>
        public static string Trouver()
        {
            DirectoryInfo? dossier = new DirectoryInfo(AppContext.BaseDirectory);

            while (dossier != null)
            {
                string candidat = Path.Combine(dossier.FullName, "modeles", NomDuDossier);

                if (Directory.Exists(candidat))
                {
                    return candidat;
                }

                dossier = dossier.Parent;
            }

            throw new DirectoryNotFoundException(
                $"L'export {NomDuDossier} est introuvable. Il n'est pas versionné : voir " +
                "backend/Pipeline/Traduction/OPUS-MT.md pour le régénérer.");
        }
    }
}
