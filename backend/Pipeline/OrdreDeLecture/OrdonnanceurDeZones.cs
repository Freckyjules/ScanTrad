using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.OrdreDeLecture
{
    /// <summary>
    /// Ordonne les blocs d'une planche par bandes horizontales : on descend de bande
    /// en bande, et à l'intérieur d'une bande on suit le sens de lecture.
    /// </summary>
    /// <remarks>
    /// Comparer deux hauteurs au pixel près ne marche pas : deux bulles côte à côte
    /// ne sont jamais <em>exactement</em> à la même hauteur sur une vraie planche, et
    /// le sens de lecture ne servirait alors jamais. Il faut une notion de « presque
    /// à la même hauteur », et c'est la hauteur des boîtes qui la donne.
    /// <para>
    /// La règle tient en une phrase : <b>un bloc reste dans la bande en cours tant que
    /// son centre tombe dans ce que la bande couvre déjà verticalement</b>. Elle n'a
    /// aucun seuil à régler — la tolérance est la taille des blocs eux-mêmes, donc
    /// elle s'adapte seule à la résolution de la planche et à la taille du texte.
    /// </para>
    /// <para>
    /// Cette tolérance ne peut pas s'écrire dans un comparateur de tri. « Presque
    /// égal » n'est pas transitif — A proche de B et B proche de C n'impose rien entre
    /// A et C — et un comparateur bâti là-dessus ne définit pas un ordre. D'où les
    /// deux temps : on groupe en bandes, puis on trie <em>dans</em> chaque bande.
    /// </para>
    /// <para>
    /// Aucune case n'est détectée, et la limite connue est là : une planche découpée
    /// en colonnes voit ses bulles s'entrelacer, alors qu'un lecteur descend une
    /// colonne entière avant de passer à l'autre. C'est le prix assumé de la
    /// simplicité — le rang reste une proposition, corrigeable depuis le front.
    /// </para>
    /// </remarks>
    public class OrdonnanceurDeZones : IOrdonnanceurDeZones
    {
        #region Méthodes

        /// <inheritdoc />
        public Planche Ordonner(Planche planche)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            List<ZoneDeTexte> ordonnees = new List<ZoneDeTexte>();

            foreach (List<ZoneDeTexte> bande in DecouperEnBandes(planche.Zones))
            {
                ordonnees.AddRange(OrdonnerLaBande(bande, planche.Sens));
            }

            for (int rang = 0; rang < ordonnees.Count; rang++)
            {
                ordonnees[rang].OrdreDeLecture = rang;
            }

            return planche.AvecZones(ordonnees);
        }

        #endregion

        #region Méthodes privées

        private static List<List<ZoneDeTexte>> DecouperEnBandes(IReadOnlyList<ZoneDeTexte> zones)
        {
            List<List<ZoneDeTexte>> bandes = new List<List<ZoneDeTexte>>();

            if (zones.Count == 0)
            {
                return bandes;
            }

            List<ZoneDeTexte> parHauteur = zones
                .OrderBy(zone => zone.Rectangle.Centre.Y)
                .ToList();

            List<ZoneDeTexte> bande = new List<ZoneDeTexte>();
            double basDeLaBande = Bas(parHauteur[0]);

            foreach (ZoneDeTexte zone in parHauteur)
            {
                // Le centre est passé sous tout ce que la bande couvrait : le bloc
                // appartient à la suite de la planche, pas à cette bande.
                if (bande.Count > 0 && zone.Rectangle.Centre.Y >= basDeLaBande)
                {
                    bandes.Add(bande);

                    bande = new List<ZoneDeTexte>();
                    basDeLaBande = Bas(zone);
                }

                bande.Add(zone);

                // On retient le point le plus bas atteint, et non celui du dernier
                // bloc vu : un grand bloc doit continuer de couvrir ceux qui le
                // suivent, sinon la bande se refermerait trop tôt.
                basDeLaBande = Math.Max(basDeLaBande, Bas(zone));
            }

            bandes.Add(bande);

            return bandes;
        }

        private static List<ZoneDeTexte> OrdonnerLaBande(List<ZoneDeTexte> bande, SensDeLecture sens)
        {
            // Le sens vient de la planche et non d'un réglage : une série retournée et
            // une série d'origine se traitent avec la même instance.
            IOrderedEnumerable<ZoneDeTexte> parCote = sens == SensDeLecture.DroiteAGauche
                ? bande.OrderByDescending(zone => zone.Rectangle.Centre.X)
                : bande.OrderBy(zone => zone.Rectangle.Centre.X);

            return parCote.ThenBy(zone => zone.Rectangle.Centre.Y).ToList();
        }

        private static double Bas(ZoneDeTexte zone)
        {
            Quadrilatere rectangle = zone.Rectangle;

            return Math.Max(rectangle.BasGauche.Y, rectangle.BasDroit.Y);
        }

        #endregion
    }
}
