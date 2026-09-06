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
    /// n'est demandé qu'ils peuvent s'exécuter en millisecondes, alors que NLLB pèse
    /// sept gigaoctets.
    /// <para>
    /// Que les moteurs réels se construisent bien par ce chemin est vérifié par les
    /// tests d'intégration, qui passent par lui.
    /// </para>
    /// </remarks>
    public class ChoixDuTraducteurTests
    {
        /// <summary>
        /// Les deux moteurs écrits sont proposés.
        /// </summary>
        [Fact]
        public void Moteurs_ContiennentLesDeuxMoteursConnus()
        {
            ChoixDuTraducteur choix = new ChoixDuTraducteur();

            Assert.Contains(MoteurDeTraduction.OpusMt, choix.Moteurs);
            Assert.Contains(MoteurDeTraduction.Nllb, choix.Moteurs);
        }

        /// <summary>
        /// Monter le choix ne charge aucun modèle.
        /// </summary>
        /// <remarks>
        /// Le test s'exécute en quelques millisecondes alors que les deux exports
        /// pèsent plus de sept gigaoctets à eux deux : c'est la preuve que rien n'est
        /// lu avant qu'on ne le demande. Sans ça, le moindre test touchant à
        /// l'aiguillage paierait le chargement des deux modèles.
        /// </remarks>
        [Fact]
        public void Constructeur_NeChargeAucunModele()
        {
            System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

            ChoixDuTraducteur choix = new ChoixDuTraducteur();

            chrono.Stop();

            Assert.NotEmpty(choix.Moteurs);
            Assert.True(
                chrono.ElapsedMilliseconds < 1000,
                $"{chrono.ElapsedMilliseconds} ms pour monter le choix : un modèle a été chargé.");
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
            ChoixDuTraducteur choix = new ChoixDuTraducteur();

            Assert.Throws<NotSupportedException>(
                () => choix.Creer((MoteurDeTraduction)999));
        }
    }
}
