# writing this script to change the path names in these xml files from
# normal/file_name to ../cmr/file_name

fileList = [ '1.xml', '10.xml', '11.xml', '12.xml', '13.xml', '14.xml',
  '15.xml', '16.xml', '17.xml', '18.xml', '19.xml', '2.xml', '20.xml',
  '21.xml', '22.xml', '23.xml', '24.xml', '25.xml', '26.xml', '27.xml',
  '28.xml', '29.xml', '3.xml', '30.xml', '31.xml', '32.xml', '33.xml',
  '34.xml', '35.xml', '36.xml', '37.xml', '38.xml', '39.xml', '4.xml',
  '40.xml', '41.xml', '42.xml', '43.xml', '44.xml', '45.xml', '46.xml',
  '47.xml', '48.xml', '49.xml', '5.xml', '50.xml', '51.xml', '52.xml',
  '53.xml', '54.xml', '55.xml', '56.xml', '57.xml', '58.xml', '59.xml',
  '6.xml', '60.xml', '61.xml', '62.xml', '7.xml', '8.xml', '9.xml' ]

for filename in fileList:
  fp = open(filename, 'r')
  lines = fp.readlines()
  fp.close()
  fp = open(filename, 'w')
  for line in lines:
    fp.write(line.replace('../cmr','../ComputerModernRoman'))
  fp.close()
