namespace ScanTrad.PipelineTests
{
    /// <summary>
    /// Donne accès à la planche et au modèle dont se servent les tests d'intégration.
    /// </summary>
    /// <remarks>
    /// La planche est recopiée à côté du binaire de test par le fichier projet. Le
    /// modèle, lui, pèse une centaine de mégaoctets et n'est pas versionné : il se
    /// cherche en remontant l'arborescence jusqu'à <c>backend/modeles/</c>.
    /// </remarks>
    public static class PlancheDEssai
    {
        /// <summary>
        /// Nom de la planche d'exemple.
        /// </summary>
        public const string Nom = "Akashic.jpg";

        private const string NomDuModele = "comictextdetector.onnx";

        /// <summary>
        /// Charge les octets de la planche d'exemple.
        /// </summary>
        /// <returns>Le contenu binaire de l'image.</returns>
        /// <exception cref="FileNotFoundException">
        /// Levée si la planche n'a pas été recopiée dans le dossier de sortie.
        /// </exception>
        public static async Task<byte[]> ChargerAsync()
        {
            string chemin = Path.Combine(AppContext.BaseDirectory, "images", Nom);

            if (!File.Exists(chemin))
            {
                throw new FileNotFoundException(
                    "La planche d'exemple est introuvable. Vérifier que le dossier images " +
                    "est bien recopié dans le dossier de sortie.", chemin);
            }

            return await File.ReadAllBytesAsync(chemin);
        }

        /// <summary>
        /// Retrouve le fichier du modèle comic-text-detector en remontant depuis le
        /// dossier d'exécution.
        /// </summary>
        /// <returns>Le chemin complet du modèle.</returns>
        /// <exception cref="FileNotFoundException">
        /// Levée si le modèle n'a pas été déposé dans <c>backend/modeles/</c>.
        /// </exception>
        public static string TrouverLeModele()
        {
            DirectoryInfo? dossier = new DirectoryInfo(AppContext.BaseDirectory);

            while (dossier != null)
            {
                string candidat = Path.Combine(dossier.FullName, "modeles", NomDuModele);

                if (File.Exists(candidat))
                {
                    return candidat;
                }

                dossier = dossier.Parent;
            }

            throw new FileNotFoundException(
                $"Le modèle {NomDuModele} est introuvable. Le déposer dans backend/modeles/ — " +
                "il n'est pas versionné, il pèse une centaine de mégaoctets.");
        }
    }
}
