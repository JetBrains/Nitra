<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:msxsl="urn:schemas-microsoft-com:xslt" exclude-result-prefixes="msxsl"
>
  <xsl:output method="text" indent="yes"/>

  <xsl:param name="inc" select="0" />

  <xsl:template match="Version">
    <xsl:variable name="version" select="number(text()) + number($inc)" />
[assembly: System.Reflection.AssemblyVersionAttribute("0.<xsl:value-of select="$version" />.0.0")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("0.<xsl:value-of select="$version" />.0.0")]
  </xsl:template>

  <xsl:template match="@* | node()">
    <xsl:copy>
      <xsl:apply-templates select="@* | node()"/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>
