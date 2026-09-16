using System.Text.Json;

namespace ScanTrad.Pipeline
{
    /// <summary>
    /// Trouve l'emplacement d'un modèle non versionné, déclaré par sa clé dans
    /// <c>modeles.local.json</c>.
    /// </summary>
    /// <remarks>
    /// Aucune convention ni aucune recherche : le fichier est lu tel quel à côté du
    /// binaire en cours d'exécution — <c>Path.Combine(AppContext.BaseDirectory,
    /// "modeles.local.json")</c> — et chaque clé doit y porter le chemin complet du
    /// modèle qu'elle désigne. C'est le fichier projet qui l'y amène, de la même
    /// façon que les images d'essai sont recopiées à côté des tests.
    /// <para>
    /// Ce fichier n'est pas versionné (voir la règle <c>*.local.json</c> du
    /// <c>.gitignore</c>, déjà utilisée pour les secrets) : chaque machine garde ses
    /// propres chemins sans jamais les écrire dans le dépôt. Un modèle
    /// <c>backend/modeles.local.json.example</c> montre la forme attendue.
    /// </para>
    /// </remarks>
    public static class LocalisateurDeModele
    {
        #region Attributs

        private static readonly Lazy<IReadOnlyDictionary<string, string?>> Configuration =
            new(ChargerLaConfiguration);

        #endregion

        #region Méthodes

        /// <summary>
        /// Trouve l'emplacement d'un modèle par sa clé de configuration.
        /// </summary>
        /// <param name="cle">La clé attendue dans <c>modeles.local.json</c>.</param>
        /// <returns>Le chemin complet du modèle, tel qu'écrit dans le fichier.</returns>
        /// <exception cref="FileNotFoundException">
        /// Levée si <c>modeles.local.json</c> est introuvable, ou si la clé n'y est pas
        /// renseignée.
        /// </exception>
        public static string Localiser(string cle)
        {
            if (Configuration.Value.TryGetValue(cle, out string? chemin) && !string.IsNullOrWhiteSpace(chemin))
            {
                return chemin;
            }

            throw new FileNotFoundException(
                $"La clé « {cle} » n'est pas renseignée dans modeles.local.json. " +
                "Voir backend/modeles.local.json.example pour la forme attendue.");
        }

        #endregion

        #region Méthodes privées

        private static Dictionary<string, string?> ChargerLaConfiguration()
        {
            string chemin = Path.Combine(AppContext.BaseDirectory, "modeles.local.json");

            if (!File.Exists(chemin))
            {
                throw new FileNotFoundException(
                    "modeles.local.json est introuvable à côté du binaire. Le copier depuis " +
                    "backend/modeles.local.json.example et y renseigner les chemins des modèles.",
                    chemin);
            }

            string contenu = File.ReadAllText(chemin);

            return JsonSerializer.Deserialize<Dictionary<string, string?>>(contenu)
                ?? new Dictionary<string, string?>();
        }

        #endregion
    }
}
