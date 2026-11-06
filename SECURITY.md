# Security

## Reporting a vulnerability

Report anything a page's visitor could exploit privately, through
[GitHub's private vulnerability reporting](https://github.com/dennisrahmen/Sedna.UI/security/advisories/new):

- script injection through the Markdown preview, the rich-text editor, the graph or any other part
  that renders the app's data;
- anything that makes the package load code or data from a remote URL;
- any problem in the catalogue's MCP server at <https://www.sedna-ui.com/mcp>.

Include the library version, the browser, and the smallest markup or request that reproduces it.

Hardening that nothing a visitor sends can trigger — a stricter default, a missing CSP note — may be a
public [issue](https://github.com/dennisrahmen/Sedna.UI/issues).

## Supported versions

Fixes ship in a new release. Only the latest release is supported.

A fix for a private report ships as a patch release with a GitHub security advisory.
