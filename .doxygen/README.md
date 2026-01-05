# Automated Documentation

This folder contains the Doxygen configuration for automated API documentation generation.

## Structure

```
.doxygen/
├── Doxyfile          # Doxygen configuration
├── .gitignore        # Excludes generated output
├── output/           # Generated documentation (gitignored)
│   └── html/         # HTML output
└── warnings.log      # Build warnings (gitignored)
```

## Local Generation

To generate documentation locally:

```bash
# From repository root
doxygen .doxygen/Doxyfile

# View the output
open .doxygen/output/html/index.html    # macOS
xdg-open .doxygen/output/html/index.html # Linux
start .doxygen/output/html/index.html    # Windows
```

## CI Integration

Documentation is automatically generated on:

- Push to `main`, `master`, or `develop` branches
- Pull requests to `main` or `master`
- Manual workflow dispatch

The workflow:

1. Generates HTML documentation using Doxygen
2. Uploads the output as a build artefact
3. Optionally deploys to GitHub Pages (on main branch)

## Enabling GitHub Pages

To enable automatic deployment to GitHub Pages:

1. Navigate to **Settings → Pages** in your repository
2. Under **Build and deployment**, select **GitHub Actions** as the source
3. The next push to main will deploy documentation to `https://<username>.github.io/<repository>/`

## Configuration

The `Doxyfile` is configured for:

- C# source extraction with XML comment parsing
- UML class diagrams via Graphviz
- MathJax for mathematical notation
- Interactive SVG diagrams
- Full source browsing with cross-references
- Tree-view navigation

### Customising Input Paths

Edit the `INPUT` directive in `Doxyfile` to match your project structure:

```
INPUT = src \
        Alaris \
        Alaris.Core \
        ...
```

### Customising Appearance

Add custom stylesheets or headers:

```
HTML_EXTRA_STYLESHEET = .doxygen/custom-stylesheet.css
HTML_HEADER           = .doxygen/custom-header.html
HTML_FOOTER           = .doxygen/custom-footer.html
```

## Requirements

- Doxygen 1.9.0 or later
- Graphviz (for diagrams)

Install on Ubuntu/Debian:

```bash
sudo apt-get install doxygen graphviz
```

Install on macOS:

```bash
brew install doxygen graphviz
```