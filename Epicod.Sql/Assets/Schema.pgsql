CREATE TABLE text_node (
	id int NOT NULL,
	parent_id int NULL,
	corpus varchar(50) NOT NULL,
	y int NOT NULL,
	x int NOT NULL,
	name varchar(200) NOT NULL,
	uri varchar(300) NULL,
	CONSTRAINT text_node_pk PRIMARY KEY (id)
);

CREATE TABLE text_node_property (
	id serial NOT NULL,
	node_id int4 NOT NULL,
	"name" varchar NOT NULL,
	value varchar NOT NULL,
	CONSTRAINT text_node_property_pk PRIMARY KEY (id)
);
CREATE INDEX text_node_property_name_idx ON public.text_node_property USING btree (name);
ALTER TABLE text_node_property ADD CONSTRAINT text_node_property_fk FOREIGN KEY (nodeid) REFERENCES text_node(id) ON DELETE CASCADE ON UPDATE CASCADE;
