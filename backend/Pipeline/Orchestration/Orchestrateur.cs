using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Orchestration
{
    /// <summary>
    /// Enchaîne six étapes déjà construites, dans l'ordre fixe du pipeline.
    /// </summary>
    /// <remarks>
    /// Ne fait rien d'autre qu'enchaîner : chaque étape reçoit la planche que la
    /// précédente a rendue. Voir <see cref="IOrchestrateurDePipeline"/> pour l'ordre
    /// et pourquoi il est ce qu'il est.
    /// </remarks>
    public class Orchestrateur : IOrchestrateurDePipeline
    {
        #region Attributs

        private readonly ILecteurDePlanche lecteur;
        private readonly IAjusteurDeRectangle ajusteur;
        private readonly IOrdonnanceurDeZones ordonnanceur;
        private readonly ITraducteur traducteur;
        private readonly IEffaceurDeTexte effaceur;
        private readonly IReecrivainDeTexte reecrivain;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise l'orchestrateur avec les six étapes qu'il enchaînera.
        /// </summary>
        /// <param name="lecteur">La première étape : détecte les zones de texte.</param>
        /// <param name="ajusteur">Maximise le rectangle de chaque zone dans sa bulle.</param>
        /// <param name="ordonnanceur">Met les zones dans l'ordre de lecture.</param>
        /// <param name="traducteur">Traduit le texte des zones ordonnées.</param>
        /// <param name="effaceur">Efface le texte d'origine des bulles.</param>
        /// <param name="reecrivain">Réécrit le texte traduit dans les bulles nettoyées.</param>
        /// <exception cref="ArgumentNullException">
        /// Levée si l'un des paramètres vaut <c>null</c>.
        /// </exception>
        public Orchestrateur(
            ILecteurDePlanche lecteur,
            IAjusteurDeRectangle ajusteur,
            IOrdonnanceurDeZones ordonnanceur,
            ITraducteur traducteur,
            IEffaceurDeTexte effaceur,
            IReecrivainDeTexte reecrivain)
        {
            this.lecteur = lecteur ?? throw new ArgumentNullException(nameof(lecteur));
            this.ajusteur = ajusteur ?? throw new ArgumentNullException(nameof(ajusteur));
            this.ordonnanceur = ordonnanceur ?? throw new ArgumentNullException(nameof(ordonnanceur));
            this.traducteur = traducteur ?? throw new ArgumentNullException(nameof(traducteur));
            this.effaceur = effaceur ?? throw new ArgumentNullException(nameof(effaceur));
            this.reecrivain = reecrivain ?? throw new ArgumentNullException(nameof(reecrivain));
        }

        #endregion

        #region Méthodes

        /// <inheritdoc />
        public async Task<Planche> TraiterAsync(Planche planche, CancellationToken jetonAnnulation = default)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            Planche lue = await lecteur.LireAsync(planche, jetonAnnulation);
            Planche cadree = ajusteur.Ajuster(lue);
            Planche ordonnee = ordonnanceur.Ordonner(cadree);

            Planche traduite = await traducteur.TraduireAsync(ordonnee, jetonAnnulation);

            Planche nettoyee = effaceur.Effacer(traduite);

            return reecrivain.Reecrire(nettoyee);
        }

        #endregion
    }
}
