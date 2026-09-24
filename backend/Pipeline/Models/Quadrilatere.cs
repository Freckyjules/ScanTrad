namespace ScanTrad.Pipeline.Models
{
    /// <summary>
    /// Les quatre coins délimitant un texte sur la page. Les coins sont
    /// indépendants les uns des autres, ce qui permet de représenter un texte
    /// incliné — c'est la forme que renvoient tous les détecteurs, et l'inclinaison
    /// doit survivre jusqu'au rendu pour réécrire le français au même angle.
    /// </summary>
    public class Quadrilatere
    {
        #region Attributs

        private Coordonnee hautGauche;
        private Coordonnee hautDroit;
        private Coordonnee basDroit;
        private Coordonnee basGauche;

        #endregion

        #region Propriétés

        /// <summary>
        /// Coin haut gauche.
        /// </summary>
        public Coordonnee HautGauche
        {
            get { return hautGauche; }
            set { hautGauche = value; }
        }

        /// <summary>
        /// Coin haut droit.
        /// </summary>
        public Coordonnee HautDroit
        {
            get { return hautDroit; }
            set { hautDroit = value; }
        }

        /// <summary>
        /// Coin bas droit.
        /// </summary>
        public Coordonnee BasDroit
        {
            get { return basDroit; }
            set { basDroit = value; }
        }

        /// <summary>
        /// Coin bas gauche.
        /// </summary>
        public Coordonnee BasGauche
        {
            get { return basGauche; }
            set { basGauche = value; }
        }

        /// <summary>
        /// Largeur du texte en pixels, mesurée le long de son inclinaison et non
        /// horizontalement. Moyenne des côtés haut et bas, qui sont rarement de
        /// longueur identique.
        /// </summary>
        public double Largeur
        {
            get { return (hautGauche.DistanceVers(hautDroit) + basGauche.DistanceVers(basDroit)) / 2; }
        }

        /// <summary>
        /// Hauteur du texte en pixels. Donne une bonne approximation de la taille
        /// de police d'origine, ce qui sert de point de départ au rendu français.
        /// </summary>
        public double Hauteur
        {
            get { return (hautGauche.DistanceVers(basGauche) + hautDroit.DistanceVers(basDroit)) / 2; }
        }

        /// <summary>
        /// Centre géométrique des quatre coins. Sert notamment à regrouper les
        /// lignes d'une même bulle et à trier les zones en ordre de lecture.
        /// </summary>
        public Coordonnee Centre
        {
            get
            {
                double sommeX = hautGauche.X + hautDroit.X + basDroit.X + basGauche.X;
                double sommeY = hautGauche.Y + hautDroit.Y + basDroit.Y + basGauche.Y;

                return new Coordonnee(sommeX / 4, sommeY / 4);
            }
        }

        /// <summary>
        /// Inclinaison du texte en degrés, obtenue en moyennant la direction des
        /// côtés haut et bas. Vaut zéro pour un texte horizontal. Une valeur
        /// positive penche vers le bas à droite, puisque l'axe vertical de l'image
        /// descend.
        /// </summary>
        public double Angle
        {
            get
            {
                double ecartX = (hautDroit.X - hautGauche.X) + (basDroit.X - basGauche.X);
                double ecartY = (hautDroit.Y - hautGauche.Y) + (basDroit.Y - basGauche.Y);

                return Math.Atan2(ecartY, ecartX) * 180 / Math.PI;
            }
        }

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise un quadrilatère dont les quatre coins sont à l'origine.
        /// </summary>
        public Quadrilatere()
        {
            hautGauche = new Coordonnee();
            hautDroit = new Coordonnee();
            basDroit = new Coordonnee();
            basGauche = new Coordonnee();
        }

        /// <summary>
        /// Initialise un quadrilatère à partir de ses quatre coins, donnés dans le
        /// sens horaire en partant du haut gauche.
        /// </summary>
        /// <param name="hautGauche">Coin haut gauche.</param>
        /// <param name="hautDroit">Coin haut droit.</param>
        /// <param name="basDroit">Coin bas droit.</param>
        /// <param name="basGauche">Coin bas gauche.</param>
        /// <exception cref="ArgumentNullException">
        /// Levée si l'un des quatre coins vaut <c>null</c>.
        /// </exception>
        public Quadrilatere(Coordonnee hautGauche, Coordonnee hautDroit, Coordonnee basDroit, Coordonnee basGauche)
        {
            this.hautGauche = hautGauche ?? throw new ArgumentNullException(nameof(hautGauche));
            this.hautDroit = hautDroit ?? throw new ArgumentNullException(nameof(hautDroit));
            this.basDroit = basDroit ?? throw new ArgumentNullException(nameof(basDroit));
            this.basGauche = basGauche ?? throw new ArgumentNullException(nameof(basGauche));
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Construit un quadrilatère droit, aligné sur les axes de l'image. Utile
        /// pour les tests, et pour les moteurs qui ne gèrent pas l'inclinaison.
        /// </summary>
        /// <param name="x">Abscisse du coin haut gauche, en pixels.</param>
        /// <param name="y">Ordonnée du coin haut gauche, en pixels.</param>
        /// <param name="largeur">Largeur en pixels.</param>
        /// <param name="hauteur">Hauteur en pixels.</param>
        /// <returns>Un quadrilatère dont les côtés sont parallèles aux bords de l'image.</returns>
        public static Quadrilatere DepuisRectangle(double x, double y, double largeur, double hauteur)
        {
            return new Quadrilatere(
                new Coordonnee(x, y),
                new Coordonnee(x + largeur, y),
                new Coordonnee(x + largeur, y + hauteur),
                new Coordonnee(x, y + hauteur));
        }

        /// <summary>
        /// Décrit les quatre coins puis les dimensions et l'inclinaison qu'ils
        /// impliquent.
        /// </summary>
        /// <returns>Une description lisible du quadrilatère.</returns>
        public override string ToString()
        {
            return FormattableString.Invariant($"HG{hautGauche} HD{hautDroit} BD{basDroit} BG{basGauche} ")
                + FormattableString.Invariant($"[{Largeur:0.#} x {Hauteur:0.#} px, {Angle:0.#}°]");
        }

        #endregion
    }
}
