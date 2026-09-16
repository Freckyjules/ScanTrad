namespace ScanTrad.PipelineTests
{
    /// <summary>
    /// Donne accès à la planche et au modèle dont se servent les tests d'intégration.
    /// </summary>
    /// <remarks>
    /// La planche est recopiée à côté du binaire de test par le fichier projet. Le
    /// modèle, lui, pèse une centaine de mégaoctets et n'est pas versionné : sa
    /// localisation passe par <see cref="ScanTrad.Pipeline.LocalisateurDeModele"/>,
    /// la même que celle utilisée pour les modèles de traduction.
    /// </remarks>
    public static class PlancheDEssai
    {
        /// <summary>
        /// Nom de la planche d'exemple.
        /// </summary>
        public const string Nom = "Akashic.jpg";

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
        /// Retrouve le fichier du modèle comic-text-detector.
        /// </summary>
        /// <returns>Le chemin complet du modèle.</returns>
        /// <exception cref="FileNotFoundException">
        /// Levée si la clé <c>detecteur</c> n'est pas renseignée dans
        /// <c>modeles.local.json</c>.
        /// </exception>
        public static string TrouverLeModele()
        {
            return ScanTrad.Pipeline.LocalisateurDeModele.Localiser("detecteur");
        }
    }
}
