using ScanTrad.Pipeline.Traduction.Moteurs;

namespace ScanTrad.PipelineTests.Traduction
{
    /// <summary>
    /// Vérifie le découpage d'un texte en phrases.
    /// </summary>
    /// <remarks>
    /// Aucun modèle n'est nécessaire : c'est du traitement de chaîne, donc ces tests
    /// s'exécutent en millisecondes. C'est précisément pour ça que le découpage vit
    /// dans sa propre classe plutôt que dans un moteur dont la construction charge
    /// sept gigaoctets.
    /// </remarks>
    public class DecoupageEnPhrasesTests
    {
        /// <summary>
        /// Deux phrases séparées par un point donnent deux morceaux, ponctuation
        /// comprise.
        /// </summary>
        [Fact]
        public void Decouper_DeuxPhrases_LesSepare()
        {
            IReadOnlyList<string> phrases =
                DecoupageEnPhrases.Decouper("The door opened. Nobody came in.");

            Assert.Equal(
                new[] { "The door opened.", "Nobody came in." },
                phrases);
        }

        /// <summary>
        /// La ponctuation enchaînée reste avec sa phrase.
        /// </summary>
        /// <remarks>
        /// C'est le cas qui compte sur une planche de manga : les bulles sont pleines
        /// de « ?! », de « ... » et de « !! ». Couper au premier signe rendrait des
        /// morceaux de ponctuation orphelins, qu'aucun modèle ne saurait traduire.
        /// </remarks>
        /// <param name="texte">Le texte à découper.</param>
        /// <param name="attendue">La première phrase attendue.</param>
        [Theory]
        [InlineData("Are you serious?! I am leaving.", "Are you serious?!")]
        [InlineData("Wait... Come back here.", "Wait...")]
        [InlineData("Stop!! You cannot pass.", "Stop!!")]
        [InlineData("Really?!! Then go.", "Really?!!")]
        public void Decouper_PonctuationEnchainee_ResteAvecSaPhrase(string texte, string attendue)
        {
            IReadOnlyList<string> phrases = DecoupageEnPhrases.Decouper(texte);

            Assert.Equal(2, phrases.Count);
            Assert.Equal(attendue, phrases[0]);
        }

        /// <summary>
        /// Un texte sans ponctuation de fin ressort entier.
        /// </summary>
        /// <remarks>
        /// Une bulle se termine souvent sans ponctuation. Rendre une liste vide ferait
        /// disparaître la réplique sans que rien ne le signale.
        /// </remarks>
        [Fact]
        public void Decouper_SansPonctuation_RendLeTexteEntier()
        {
            IReadOnlyList<string> phrases = DecoupageEnPhrases.Decouper("No punctuation here");

            Assert.Equal("No punctuation here", Assert.Single(phrases));
        }

        /// <summary>
        /// Ce qui suit la dernière ponctuation forme une phrase, même inachevée.
        /// </summary>
        [Fact]
        public void Decouper_TexteApresLaDernierePonctuation_LeGarde()
        {
            IReadOnlyList<string> phrases =
                DecoupageEnPhrases.Decouper("She left. And then");

            Assert.Equal(new[] { "She left.", "And then" }, phrases);
        }

        /// <summary>
        /// Une seule phrase reste une seule phrase.
        /// </summary>
        [Fact]
        public void Decouper_UnePhraseUnique_NeLaCoupePas()
        {
            IReadOnlyList<string> phrases = DecoupageEnPhrases.Decouper("Just one sentence.");

            Assert.Equal("Just one sentence.", Assert.Single(phrases));
        }

        /// <summary>
        /// Les espaces entre les phrases ne se retrouvent pas dans les morceaux.
        /// </summary>
        [Fact]
        public void Decouper_EspacesMultiples_NeLesGardePas()
        {
            IReadOnlyList<string> phrases =
                DecoupageEnPhrases.Decouper("First.    Second.");

            Assert.Equal(new[] { "First.", "Second." }, phrases);
        }

        /// <summary>
        /// Un texte vide ne produit pas de morceau vide à envoyer au modèle.
        /// </summary>
        [Fact]
        public void Decouper_TexteVide_RendUnSeulElement()
        {
            Assert.Equal(string.Empty, Assert.Single(DecoupageEnPhrases.Decouper(string.Empty)));
        }

        /// <summary>
        /// Découper rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public void Decouper_Null_LeveArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => DecoupageEnPhrases.Decouper(null!));
        }
    }
}
