using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Traduction;

namespace ScanTrad.PipelineTests.Traduction
{
    /// <summary>
    /// Vérifie l'aiguillage entre les moteurs de traduction.
    /// </summary>
    /// <remarks>
    /// Aucun modèle n'est nécessaire ici : ces tests n'éprouvent que le choix, jamais
    /// la traduction. C'est justement parce que rien n'est construit tant que rien
    /// n'est demandé qu'ils peuvent s'exécuter en millisecondes.
    /// <para>
    /// Que le moteur réel se construise bien par ce chemin est vérifié par
    /// <see cref="TraductionOpusMtTests"/>, qui passe par lui.
    /// </para>
    /// </remarks>
    public class ChoixDuTraducteurTests
    {
        private const string DossierQuelconque = @"C:\modeles\opus-mt-en-fr";

        /// <summary>
        /// OPUS-MT fait partie des moteurs connus.
        /// </summary>
        [Fact]
        public void Moteurs_ContientOpusMt()
        {
            ChoixDuTraducteur choix = new ChoixDuTraducteur(DossierQuelconque);

            Assert.Equal(MoteurDeTraduction.OpusMt, Assert.Single(choix.Moteurs));
        }

        /// <summary>
        /// Monter le choix ne charge aucun modèle.
        /// </summary>
        /// <remarks>
        /// Le dossier indiqué n'existe pas : si le constructeur tentait d'ouvrir le
        /// modèle, il échouerait. C'est ce qui permet de déclarer des moteurs pesant
        /// des centaines de mégaoctets sans rien payer tant que personne ne les
        /// demande.
        /// </remarks>
        [Fact]
        public void Constructeur_NeChargeAucunModele()
        {
            ChoixDuTraducteur choix = new ChoixDuTraducteur(@"C:\dossier\qui\n\existe\pas");

            Assert.NotEmpty(choix.Moteurs);
        }

        /// <summary>
        /// Un moteur inconnu est signalé, et non remplacé en silence par un autre.
        /// </summary>
        /// <remarks>
        /// Il n'existe pas de traduction « neutre » vers laquelle se rabattre : rendre
        /// du français produit par un moteur que l'utilisateur n'a pas choisi serait
        /// pire qu'une erreur franche.
        /// </remarks>
        [Fact]
        public void Creer_MoteurInconnu_LeveNotSupportedException()
        {
            ChoixDuTraducteur choix = new ChoixDuTraducteur(DossierQuelconque);

            Assert.Throws<NotSupportedException>(
                () => choix.Creer((MoteurDeTraduction)999));
        }

        /// <summary>
        /// Un choix sans dossier de modèle n'a pas de sens.
        /// </summary>
        [Fact]
        public void Constructeur_DossierVide_LeveArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ChoixDuTraducteur("  "));
        }
    }
}
