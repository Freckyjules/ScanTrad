using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Models
{
    /// <summary>
    /// Vérifie le type couleur du modèle.
    /// </summary>
    public class CouleurTests
    {
        /// <summary>
        /// Les trois composantes sont retenues telles quelles, dans l'ordre où on les
        /// nomme.
        /// </summary>
        [Fact]
        public void Constructeur_AvecTroisComposantes_LesRetient()
        {
            Couleur couleur = new Couleur(250, 240, 230);

            Assert.Equal(250, couleur.Rouge);
            Assert.Equal(240, couleur.Vert);
            Assert.Equal(230, couleur.Bleu);
        }

        /// <summary>
        /// Une couleur non renseignée est noire, pas indéterminée.
        /// </summary>
        [Fact]
        public void Constructeur_SansArgument_DonneDuNoir()
        {
            Couleur couleur = new Couleur();

            Assert.Equal(0, couleur.Rouge);
            Assert.Equal(0, couleur.Vert);
            Assert.Equal(0, couleur.Bleu);
        }

        /// <summary>
        /// Une composante hors de 0-255 ne décrit aucune couleur et doit être
        /// signalée à la construction, plutôt que de ressortir en pixel absurde bien
        /// plus loin.
        /// </summary>
        /// <param name="rouge">Composante rouge essayée.</param>
        /// <param name="vert">Composante verte essayée.</param>
        /// <param name="bleu">Composante bleue essayée.</param>
        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(256, 0, 0)]
        [InlineData(0, -1, 0)]
        [InlineData(0, 256, 0)]
        [InlineData(0, 0, -1)]
        [InlineData(0, 0, 256)]
        public void Constructeur_ComposanteHorsBornes_LeveArgumentOutOfRange(
            int rouge, int vert, int bleu)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Couleur(rouge, vert, bleu));
        }

        /// <summary>
        /// Les bornes elles-mêmes sont valides.
        /// </summary>
        [Fact]
        public void Constructeur_AuxBornes_Accepte()
        {
            Couleur noir = new Couleur(0, 0, 0);
            Couleur blanc = new Couleur(255, 255, 255);

            Assert.Equal(0, noir.Rouge);
            Assert.Equal(255, blanc.Bleu);
        }

        /// <summary>
        /// La description donne les composantes puis le code hexadécimal, sous lequel
        /// le front manipulera la couleur.
        /// </summary>
        [Fact]
        public void ToString_DonneLesComposantesEtLeCodeHexadecimal()
        {
            Couleur couleur = new Couleur(250, 240, 230);

            Assert.Equal("RVB(250, 240, 230) #FAF0E6", couleur.ToString());
        }
    }
}
