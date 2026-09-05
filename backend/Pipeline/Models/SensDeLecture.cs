namespace ScanTrad.Pipeline.Models
{
    /// <summary>
    /// Sens dans lequel se parcourent les cases et les bulles d'une planche.
    /// </summary>
    /// <remarks>
    /// Un manga japonais se lit de droite à gauche. Mais beaucoup d'éditions
    /// anglaises sont publiées retournées pour se lire de gauche à droite, et vous
    /// croiserez les deux : le sens doit donc se choisir planche par planche, jamais
    /// être écrit en dur.
    /// </remarks>
    public enum SensDeLecture
    {
        /// <summary>
        /// Sens du manga d'origine : dans une bande de cases, on commence par celle
        /// de droite.
        /// </summary>
        DroiteAGauche,

        /// <summary>
        /// Sens occidental, celui des éditions retournées et des bandes dessinées
        /// européennes : dans une bande de cases, on commence par celle de gauche.
        /// </summary>
        GaucheADroite
    }
}
