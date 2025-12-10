# Alaris Mathematical Specification

This directory contains the formal mathematical specification of the Alaris quantitative trading system. The document derives the complete theoretical foundation for option pricing under both positive and negative interest rate regimes, specifies the earnings-based volatility trading strategy with mathematically precise entry criteria, and formalises the fault monitoring framework as a system of inequalities and logical predicates.

## Overview

The specification begins from first principles with the risk-neutral stochastic differential equation governing asset prices and develops the free boundary formulation for American option pricing. It extends this framework to the double boundary regime that arises under negative interest rates, where the optimal exercise region becomes a bounded interval rather than a half-line. The document then formalises the earnings volatility calendar spread strategy, expressing trading signals as evaluable predicates derived from academic literature. Finally, it specifies the complete fault detection system that monitors data quality, model validity, execution risk, and position risk.

## Building the Documentation

The specification is written in Quarto markdown and renders to both HTML and PDF formats. To build the documentation, ensure you have Quarto installed with XeLaTeX support, then run the following from this directory:

1. Navigate to the Documentation directory.
2. Execute `quarto render Alaris.qmd` to generate both HTML and PDF outputs.
3. The rendered files will appear as `Alaris.html` and `Alaris.pdf`.

## File Structure

The `Alaris.qmd` file is the primary source document. The `References.bib` file contains the bibliography in BibTeX format, covering American option pricing, volatility estimation, earnings announcements, and related financial literature. The `Finance.csl` file specifies the citation style, and `Styling.scss` provides custom styling for the HTML output. The `Literature` subdirectory contains reference materials used during document preparation.

## Dependencies

Rendering requires Quarto 1.3 or later with a working TeX installation. The PDF output uses XeLaTeX with the microtype, booktabs, amsmath, amssymb, mathtools, tikz, and pgfplots packages. These are typically included in a full TeX Live installation.
