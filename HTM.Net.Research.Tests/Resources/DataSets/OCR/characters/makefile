document = characters
last_image_saved = Yuanti/lowercase_z.jpg

${last_image_saved}: parseJPG.py ${document}.jpg ${document}.xml
	python2 parseJPG.py

${document}.xml: fonts.txt ${document}.pdf
	python3 makexml.py

${document}.jpg: ${document}.pdf
	convert -density 150 ${document}.pdf -quality 90 ${document}.jpg

${document}.pdf: ${document}.tex
	xelatex ${document}.tex

${document}.tex: fonts.txt maketex.py
	python3 maketex.py

