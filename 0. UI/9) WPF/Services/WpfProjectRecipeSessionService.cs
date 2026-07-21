namespace MvcVisionSystem
{
    /// <summary>
    /// Performs the non-visual Core transitions for the active recipe. The
    /// shell remains responsible for UI refresh and operator-facing status.
    /// </summary>
    public sealed class WpfProjectRecipeSessionService
    {
        public string Save(CData data, string recipeName)
        {
            CRecipe.InitDirectory(recipeName);
            data.SaveConfig(recipeName);
            return WpfProjectRecipeService.BuildConfigPath(
                WpfProjectRecipeService.GetRecipeRootDirectory(),
                recipeName);
        }

        public string Apply(CRecipe recipe, string recipeName)
        {
            string previousRecipeName = (recipe.Name ?? string.Empty).Trim();
            recipe.Name = recipeName;
            return previousRecipeName;
        }
    }
}
