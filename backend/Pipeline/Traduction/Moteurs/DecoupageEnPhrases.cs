namespace ScanTrad.Pipeline.Traduction.Moteurs
{
    /// <summary>
    /// Découpe un texte en phrases, pour les modèles qui n'en traitent qu'une à la
    /// fois.
    /// </summary>
    /// <remarks>
    /// Les modèles de traduction neuronale sont entraînés sur des paires de phrases.
    /// Une entrée qui en contient deux ressort souvent amputée de la seconde : le
    /// modèle traduit la première, produit sa marque de fin, et considère avoir
    /// terminé. Découper avant de traduire est la façon correcte de s'en servir.
    /// <para>
    /// La règle : on accumule des mots jusqu'à une ponctuation de fin, puis on avale
    /// toute la ponctuation qui suit avant de couper. C'est ce qui garde
    /// <c>« ?! »</c>, <c>« ... »</c> et <c>« !! »</c> d'un seul tenant — une planche
    /// de manga en est pleine.
    /// </para>
    /// <para>
    /// Aucune tentative de reconnaître les abréviations : sur du dialogue en
    /// majuscules, les faux positifs seraient rares et le remède plus coûteux que le
    /// mal.
    /// </para>
    /// </remarks>
    public static class DecoupageEnPhrases
    {
        #region Méthodes

        /// <summary>
        /// Découpe un texte en phrases, ponctuation comprise.
        /// </summary>
        /// <param name="texte">Le texte à découper.</param>
        /// <returns>
        /// Les phrases, dans l'ordre. Un texte sans ponctuation de fin ressort entier,
        /// en une seule phrase : il n'y a jamais moins d'un élément.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="texte"/> vaut <c>null</c>.
        /// </exception>
        public static IReadOnlyList<string> Decouper(string texte)
        {
            if (texte == null)
            {
                throw new ArgumentNullException(nameof(texte));
            }

            List<string> phrases = new List<string>();
            int debut = 0;

            for (int rang = 0; rang < texte.Length; rang++)
            {
                if (!EstUneFinDePhrase(texte[rang]))
                {
                    continue;
                }

                // Toute la ponctuation qui suit appartient à la même phrase.
                int fin = rang;

                while (fin + 1 < texte.Length && EstUneFinDePhrase(texte[fin + 1]))
                {
                    fin++;
                }

                Ajouter(phrases, texte.Substring(debut, fin - debut + 1));

                debut = fin + 1;
                rang = fin;
            }

            // Ce qui suit la dernière ponctuation est une phrase aussi, même sans
            // ponctuation finale — une bulle se termine souvent sans.
            if (debut < texte.Length)
            {
                Ajouter(phrases, texte.Substring(debut));
            }

            return phrases.Count == 0 ? new[] { texte } : phrases;
        }

        #endregion

        #region Méthodes privées

        private static bool EstUneFinDePhrase(char caractere)
        {
            return caractere is '.' or '!' or '?' or '…';
        }

        private static void Ajouter(List<string> phrases, string phrase)
        {
            string propre = phrase.Trim();

            if (propre.Length > 0)
            {
                phrases.Add(propre);
            }
        }

        #endregion
    }
}
