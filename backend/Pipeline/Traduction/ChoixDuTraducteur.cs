using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Traduction.OpusMt;

namespace ScanTrad.Pipeline.Traduction
{
    /// <summary>
    /// Construit le traducteur correspondant au moteur demandé.
    /// </summary>
    /// <remarks>
    /// Le dictionnaire des moteurs est monté dans le constructeur : la liste est
    /// close, elle se lit d'un coup d'œil, et ajouter un moteur revient à ajouter une
    /// ligne. C'est aussi ce qui évite d'avoir à enregistrer quoi que ce soit depuis
    /// l'extérieur.
    /// <para>
    /// Les valeurs sont des fonctions et non des traducteurs déjà construits. C'est le
    /// seul écart avec un aiguillage ordinaire, et il est imposé par le coût : un
    /// moteur local charge un demi-gigaoctet de modèle en un peu plus d'une seconde.
    /// Tous les construire d'avance chargerait chaque modèle au démarrage, y compris
    /// ceux dont personne ne se servira, et obligerait cette classe à les libérer.
    /// Ici, rien n'est chargé tant que rien n'est demandé.
    /// </para>
    /// <para>
    /// Un moteur inconnu lève plutôt que de se rabattre sur un autre : il n'existe pas
    /// de traduction « neutre » vers laquelle se replier, et rendre du français produit
    /// par un moteur que l'utilisateur n'a pas choisi serait pire qu'une erreur franche.
    /// </para>
    /// </remarks>
    public class ChoixDuTraducteur
    {
        #region Attributs

        private Dictionary<MoteurDeTraduction, Func<ITraducteur>> moteurs;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Crée le choix des traducteurs et monte la liste des moteurs connus.
        /// </summary>
        /// <param name="dossierOpusMt">
        /// Dossier de l'export ONNX d'OPUS-MT. Il n'est lu qu'au moment où ce moteur
        /// est réellement demandé.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="dossierOpusMt"/> est vide.
        /// </exception>
        public ChoixDuTraducteur(string dossierOpusMt)
        {
            if (string.IsNullOrWhiteSpace(dossierOpusMt))
            {
                throw new ArgumentException(
                    "Le dossier du modèle OPUS-MT doit être renseigné.", nameof(dossierOpusMt));
            }

            this.moteurs = new Dictionary<MoteurDeTraduction, Func<ITraducteur>>
            {
                { MoteurDeTraduction.OpusMt, () => new TraductionOpusMt(dossierOpusMt) }
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
