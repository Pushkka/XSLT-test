<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:output method="xml" indent="yes" encoding="utf-8"/>

	<xsl:template match="/Pay">
		<Employees>
			<xsl:for-each-group select="//item" group-by="concat(@name, '|', @surname)">
				<Employee name="{current-group()[1]/@name}" surname="{current-group()[1]/@surname}">
					<xsl:for-each select="current-group()">
						<salary amount="{@amount}" mount="{name(..)}"/>
					</xsl:for-each>
				</Employee>
			</xsl:for-each-group>
		</Employees>
	</xsl:template>
</xsl:stylesheet>
