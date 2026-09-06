using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Traduction.Moteurs.Nllb;
using ScanTrad.Pipeline.Traduction.Moteurs.OpusMt;

namespace ScanTrad.Pipeline.Traduction
{
    /// <summary>
    /// Construit le traducteur correspondant au moteur demandé.
    /// </summary>
    /// <remarks>
    /// Le dictionnaire des moteurs est monté dans le constructeur : la liste est
    /// close, elle se lit d'un coup d'œil, et ajouter un moteur revient à ajouter une
    /// ligne.
    /// <para>
    /// <b>Les emplacements des modèles sont écrits en dur</b>, et c'est provisoire.
    /// Celui de NLLB est un chemin absolu propre à la machine de développement — son
    /// export pèse sept gigaoctets et ne tenait pas sur le disque système. Le jour où
    /// ce projet tournera ailleurs, ces chemins devront venir de la configuration.
    /// </para>
    /// <para>
    /// Les valeurs sont des fonctions et non des traducteurs déjà construits. C'est le
    /// seul écart avec un aiguillage ordinaire, et il est imposé par le coût : NLLB
    /// met sept secondes à se charger et occupe sept gigaoctets. Tous les construire
    /// d'avance chargerait chaque modèle au démarrage, y compris ceux dont personne ne
    /// se servira. Ici, rien n'est chargé tant que rien n'est demandé.
    /// </para>
    /// <para>
    /// Un moteur inconnu lève plutôt que de se rabattre sur un autre : il n'existe pas
    /// de traduction « neutre » vers laquelle se replier, et rendre du français produit
    /// par un moteur que l'utilisateur n'a pas choisi serait pire qu'une erreur franche.
    /// </para>
    /// </remarks>
    public class ChoixDuTraducteur
    {
        #region Constantes

        /// <summary>
        /// Emplacement de l'export OPUS-MT. Voir le mémo du moteur pour le régénérer.
        /// </summary>
        private const string DossierOpusMt =
            @"C:\Users\jules\Documents\GitHub\ScanTrad\backend\modeles\opus-mt-en-fr";

        /// <summary>
        /// Emplacement de l'export NLLB-200. Hors du dépôt : ses sept gigaoctets ne
        /// tenaient pas sur le disque système.
        /// </summary>
        private const string DossierNllb =
            @"E:\ScanTrad-modeles\nllb-200-distilled-600M";

        #endregion

        #region Attributs

        private Dictionary<MoteurDeTraduction, Func<ITraducteur>> moteurs;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Crée le choix des traducteurs et monte la liste des moteurs connus. Aucun
        /// modèle n'est lu à ce moment.
        /// </summary>
        public ChoixDuTraducteur()
        {
            this.moteurs = new Dictionary<MoteurDeTraduction, Func<ITraducteur>>
            {
                { MoteurDeTraduction.OpusMt, () => new TraductionOpusMt(DossierOpusMt) },
                { MoteurDeTraduction.Nllb, () => new TraductionNllb(DossierNllb) }
            };
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// Les moteurs que ce choix sait construire.
        /// </summary>
        public IReadOnlyCollection<MoteurDeTraduction> Moteurs
        {
            get { return this.moteurs.Keys.ToArray(); }
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Construit le traducteur du moteur demandé.
        /// </summary>
        /// <param name="moteur">Le moteur voulu.</param>
        /// <returns>
        /// Un traducteur neuf, dont l'appelant devient responsable : c'est à lui de le
        /// libérer. Charger un modèle coûte des secondes, donc on en construit un pour
        /// tout un chapitre et non un par planche.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Levée si le moteur demandé n'est pas connu.
        /// </exception>
        public ITraducteur Creer(MoteurDeTraduction moteur)
        {
            if (!this.moteurs.ContainsKey(moteur))
            {
                throw new NotSupportedException(
                    $"Le moteur de traduction {moteur} n'est pas connu. " +
                    $"Moteurs disponibles : {string.Join(", ", this.moteurs.Keys)}.");
            }

            return this.moteurs[moteur]();
        }

        #endregion
    }
}
